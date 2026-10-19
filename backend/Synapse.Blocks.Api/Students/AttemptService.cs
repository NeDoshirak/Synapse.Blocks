using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Contracts.Students;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Api.MarkdownPages;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Students;

public sealed class AttemptService(AppDbContext db, MarkdownRenderer markdownRenderer)
{
    public async Task<(ParticipantDto Participant, string ContinuationToken)?> CreateOrContinueParticipantAsync(
        Guid caseId, string? displayName, string? continuationToken, CancellationToken cancellationToken = default)
    {
        if (!StudentNameNormalizer.TryNormalize(displayName, out var normalizedDisplay, out var normalizedKey)) return null;
        var existing = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (existing is not null && existing.NormalizedName == normalizedKey)
            return (new ParticipantDto(existing.Id, existing.DisplayName), continuationToken!);

        var rawToken = CreateToken();
        var participant = new Participant
        {
            CaseId = caseId,
            DisplayName = normalizedDisplay,
            NormalizedName = normalizedKey,
            ContinuationHash = Hash(rawToken)
        };
        db.Participants.Add(participant);
        await db.SaveChangesAsync(cancellationToken);
        return (new ParticipantDto(participant.Id, participant.DisplayName), rawToken);
    }

    public async Task<AttemptDto?> StartAttemptAsync(
        Guid caseId,
        string shareToken,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var participant = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (participant is null) return null;
        var set = await db.TeacherCases.AsNoTracking()
            .Include(item => item.Sections).ThenInclude(section => section.Items).ThenInclude(content => content.LevelVersion)
            .Include(item => item.Sections).ThenInclude(section => section.Items).ThenInclude(content => content.MarkdownPageVersion)
            .SingleOrDefaultAsync(item => item.Id == caseId && item.IsPublished && !item.Archived, cancellationToken);
        if (set is null) return null;

        var orderedItems = set.Sections.OrderBy(section => section.Order)
            .SelectMany(section => section.Items.OrderBy(item => item.Order).Select(item => (section, item)))
            .ToArray();
        if (orderedItems.Length is < 1 or > 100) return null;
        var levels = orderedItems.Where(entry => entry.item.Kind == CaseContentItem.LevelKind).ToArray();
        var levelVersionIds = levels.Select(entry => entry.item.LevelVersionId!.Value).ToList();
        var presentation = levels.Select(entry => new LevelPresentation(
            entry.item.LevelVersionId!.Value,
            entry.section.Order,
            entry.section.Title,
            EffectiveTitle(entry.item.DisplayTitle, entry.item.LevelVersion!.Title))).ToArray();
        var attempt = new Attempt
        {
            CaseId = caseId,
            ParticipantId = participant.Id,
            ProgressionMode = set.ProgressionMode,
            LevelVersionIdsJson = JsonSerializer.Serialize(levelVersionIds),
            LevelPresentationJson = JsonSerializer.Serialize(presentation, JsonOptions)
        };
        attempt.Steps = orderedItems.Select((entry, index) => new AttemptStep
        {
            Attempt = attempt,
            SourceItemId = entry.item.Id,
            Order = index + 1,
            Kind = entry.item.Kind,
            SectionOrder = entry.section.Order,
            SectionTitle = entry.section.Title,
            Title = EffectiveTitle(entry.item.DisplayTitle,
                entry.item.LevelVersion?.Title ?? entry.item.MarkdownPageVersion?.Title ?? ""),
            LevelVersionId = entry.item.LevelVersionId,
            MarkdownPageVersionId = entry.item.MarkdownPageVersionId,
            Markdown = entry.item.Kind == CaseContentItem.MarkdownKind ? entry.item.MarkdownPageVersion!.Markdown : null
        }).ToList();
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(attempt, shareToken, cancellationToken);
    }

    public async Task<AttemptDto?> GetCurrentAsync(
        Guid caseId,
        string shareToken,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var participant = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (participant is null) return null;
        var attempt = await db.Attempts.AsNoTracking().Include(item => item.LevelResults).Include(item => item.Steps)
            .Where(item => item.CaseId == caseId && item.ParticipantId == participant.Id)
            .OrderByDescending(item => item.StartedAt).FirstOrDefaultAsync(cancellationToken);
        return attempt is null ? null : await ToDtoAsync(attempt, shareToken, cancellationToken);
    }

    public async Task<AttemptDto?> CompleteMarkdownStepAsync(
        Guid caseId,
        Guid attemptId,
        Guid stepId,
        string shareToken,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [attemptId.ToString()], cancellationToken);
        var attempt = await FindAttemptAsync(caseId, attemptId, continuationToken, cancellationToken);
        if (attempt is null) return null;
        var step = attempt.Steps.SingleOrDefault(item => item.Id == stepId && item.Kind == CaseContentItem.MarkdownKind);
        if (step is null) return null;

        if (!step.Completed && attempt.ProgressionMode == CaseProgressionMode.Sequential)
        {
            var next = attempt.Steps.OrderBy(item => item.Order).FirstOrDefault(item => !item.Completed);
            if (next?.Id != step.Id) throw new AttemptProgressConflictException();
        }

        step.Completed = true;
        step.CompletedAt ??= DateTimeOffset.UtcNow;
        if (attempt.Steps.Count > 0 && attempt.Steps.All(item => item.Completed))
        {
            attempt.Status = "Completed";
            attempt.CompletedAt ??= DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(attempt, shareToken, cancellationToken);
    }

    internal async Task<Attempt?> FindAttemptAsync(Guid caseId, Guid attemptId, string? continuationToken, CancellationToken cancellationToken)
    {
        var participant = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (participant is null) return null;
        return await db.Attempts.Include(item => item.LevelResults).Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.Id == attemptId && item.CaseId == caseId && item.ParticipantId == participant.Id, cancellationToken);
    }

    private async Task<Participant?> FindParticipantAsync(Guid caseId, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43) return null;
        var hash = Hash(token);
        var participant = await db.Participants.FirstOrDefaultAsync(item => item.CaseId == caseId && item.ContinuationHash == hash, cancellationToken);
        if (participant is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(participant.ContinuationHash), Convert.FromHexString(hash))) return null;
        return participant;
    }

    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string CreateToken() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private async Task<AttemptDto> ToDtoAsync(Attempt attempt, string shareToken, CancellationToken cancellationToken)
    {
        var versionIds = JsonSerializer.Deserialize<List<Guid>>(attempt.LevelVersionIdsJson) ?? [];
        var presentation = JsonSerializer.Deserialize<List<LevelPresentation>>(attempt.LevelPresentationJson, JsonOptions) ?? [];
        var presentationByVersionId = presentation.GroupBy(item => item.LevelVersionId)
            .ToDictionary(group => group.Key, group => group.First());
        var completedIds = attempt.LevelResults.Where(item => item.Completed).Select(item => item.LevelVersionId).ToHashSet();
        var versions = await db.LevelVersions.Where(item => versionIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var levels = new List<StudentCaseLevelDto>(versionIds.Count);
        for (var order = 0; order < versionIds.Count; order++)
        {
            var versionId = versionIds[order];
            if (!versions.TryGetValue(versionId, out var version)) continue;
            var definition = JsonSerializer.Deserialize<LevelDefinition>(version.DefinitionJson, JsonOptions)
                ?? throw new InvalidOperationException("Level version snapshot is invalid.");
            definition.Tests = definition.Tests.Where(test => !test.Hidden).ToList();
            presentationByVersionId.TryGetValue(versionId, out var savedPresentation);
            levels.Add(new StudentCaseLevelDto(versionId, order + 1, savedPresentation?.SectionOrder,
                savedPresentation?.SectionTitle, EffectiveTitle(savedPresentation?.DisplayTitle, version.Title), definition));
        }

        var steps = new List<StudentCaseStepDto>();
        if (attempt.Steps.Count > 0)
        {
            var stepLevelIds = attempt.Steps.Where(item => item.LevelVersionId is not null)
                .Select(item => item.LevelVersionId!.Value).Distinct().ToArray();
            var stepVersions = await db.LevelVersions.Where(item => stepLevelIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            foreach (var step in attempt.Steps.OrderBy(item => item.Order))
            {
                LevelDefinition? definition = null;
                string? html = null;
                if (step.Kind == CaseContentItem.LevelKind && step.LevelVersionId is { } levelVersionId && stepVersions.TryGetValue(levelVersionId, out var stepVersion))
                {
                    definition = JsonSerializer.Deserialize<LevelDefinition>(stepVersion.DefinitionJson, JsonOptions)
                        ?? throw new InvalidOperationException("Level definition snapshot is invalid.");
                    definition.Tests = definition.Tests.Where(test => !test.Hidden).ToList();
                }
                else if (step.Kind == CaseContentItem.MarkdownKind && step.Markdown is not null)
                {
                    html = markdownRenderer.Render(step.Markdown, assetId =>
                        $"/api/student/cases/{shareToken}/attempts/{attempt.Id}/assets/{assetId:D}/content");
                }
                steps.Add(new StudentCaseStepDto(step.Id, step.Kind, step.Order, step.SectionOrder,
                    step.SectionTitle, step.Title, step.LevelVersionId, definition, html, step.Completed));
            }
        }
        else
        {
            // Historical attempts only stored levels; reconstruct that original level-only sequence.
            foreach (var level in levels)
                steps.Add(new StudentCaseStepDto(level.LevelVersionId, CaseContentItem.LevelKind, level.Order,
                    level.SectionOrder, level.SectionTitle, level.DisplayTitle, level.LevelVersionId,
                    level.Definition, null, completedIds.Contains(level.LevelVersionId)));
        }

        var completedStepCount = attempt.Steps.Count > 0
            ? attempt.Steps.Count(item => item.Completed)
            : attempt.LevelResults.Count(item => item.Completed);
        return new AttemptDto(attempt.Id, attempt.Status, attempt.StartedAt, attempt.CompletedAt,
            attempt.LevelResults.Count(item => item.Completed), completedStepCount, attempt.ProgressionMode,
            versionIds, completedIds.ToArray(), levels, steps);
    }

    private sealed record LevelPresentation(Guid LevelVersionId, int? SectionOrder, string? SectionTitle, string? DisplayTitle);

    private static string EffectiveTitle(string? displayTitle, string canonicalTitle)
        => string.IsNullOrWhiteSpace(displayTitle) ? canonicalTitle : displayTitle;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}
