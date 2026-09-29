using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Students;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Students;

public sealed class AttemptService(AppDbContext db)
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

    public async Task<AttemptDto?> StartAttemptAsync(Guid caseId, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var participant = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (participant is null) return null;
        var levelVersionIds = await db.CaseLevels.AsNoTracking().Where(item => item.CaseId == caseId).OrderBy(item => item.Order).Select(item => item.LevelVersionId).ToListAsync(cancellationToken);
        if (levelVersionIds.Count == 0) return null;
        var attempt = new Attempt { CaseId = caseId, ParticipantId = participant.Id, LevelVersionIdsJson = JsonSerializer.Serialize(levelVersionIds) };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(attempt, 0, cancellationToken);
    }

    public async Task<AttemptDto?> GetCurrentAsync(Guid caseId, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var participant = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (participant is null) return null;
        var attempt = await db.Attempts.AsNoTracking().Include(item => item.LevelResults)
            .Where(item => item.CaseId == caseId && item.ParticipantId == participant.Id)
            .OrderByDescending(item => item.StartedAt).FirstOrDefaultAsync(cancellationToken);
        return attempt is null ? null : await ToDtoAsync(attempt, attempt.LevelResults.Count(result => result.Completed), cancellationToken);
    }

    internal async Task<Attempt?> FindAttemptAsync(Guid caseId, Guid attemptId, string? continuationToken, CancellationToken cancellationToken)
    {
        var participant = await FindParticipantAsync(caseId, continuationToken, cancellationToken);
        if (participant is null) return null;
        return await db.Attempts.Include(item => item.LevelResults)
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
    private async Task<AttemptDto> ToDtoAsync(Attempt attempt, int completedLevelCount, CancellationToken cancellationToken)
    {
        var versionIds = JsonSerializer.Deserialize<List<Guid>>(attempt.LevelVersionIdsJson) ?? [];
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
            levels.Add(new StudentCaseLevelDto(versionId, order, definition));
        }
        return new AttemptDto(attempt.Id, attempt.Status, attempt.StartedAt, attempt.CompletedAt, completedLevelCount, versionIds, completedIds.ToArray(), levels);
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}
