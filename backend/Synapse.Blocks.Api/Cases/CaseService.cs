using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Api.MarkdownPages;
using Synapse.Blocks.Api.Contracts.Students;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Cases;

public sealed class CaseService(AppDbContext db)
{
    public const string OrderedLevels = "orderedLevels";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<IReadOnlyList<TeacherCaseDto>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var items = await OwnedCaseQuery(ownerId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(ToTeacherDto).ToArray();
    }

    public async Task<TeacherCaseDto?> GetAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var item = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        return item is null ? null : ToTeacherDto(item);
    }

    public async Task<TeacherCaseDto> CreateAsync(string ownerId, CreateCaseRequest request, CancellationToken cancellationToken = default)
    {
        var sections = await ValidateAndLoadSectionsAsync(ownerId, request.CaseType, request.Title, request.Description,
            request.Sections, request.ProgressionMode, cancellationToken);
        var entity = new TeacherCase
        {
            OwnerId = ownerId,
            CaseType = OrderedLevels,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            ProgressionMode = request.ProgressionMode,
            Sections = BuildSections(Guid.Empty, sections)
        };

        foreach (var section in entity.Sections)
        {
            section.Case = entity;
            section.CaseId = entity.Id;
            foreach (var content in section.Items)
            {
                content.Case = entity;
                content.CaseId = entity.Id;
            }
        }

        db.TeacherCases.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToTeacherDto(await OwnedCaseQuery(ownerId).SingleAsync(item => item.Id == entity.Id, cancellationToken));
    }

    public async Task<TeacherCaseDto?> UpdateAsync(string ownerId, Guid caseId, UpdateCaseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.TeacherCases.Include(item => item.ShareLinks)
            .FirstOrDefaultAsync(item => item.Id == caseId && item.OwnerId == ownerId, cancellationToken);
        if (entity is null || entity.Archived) return null;

        // Validate every reference before replacing any saved section or item.
        var sections = await ValidateAndLoadSectionsAsync(ownerId, request.CaseType, request.Title, request.Description,
            request.Sections, request.ProgressionMode, cancellationToken);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await db.CaseContentItems.Where(item => item.CaseId == caseId).ExecuteDeleteAsync(cancellationToken);
        await db.CaseSections.Where(section => section.CaseId == caseId).ExecuteDeleteAsync(cancellationToken);

        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.CaseType = OrderedLevels;
        entity.ProgressionMode = request.ProgressionMode;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var replacementSections = BuildSections(caseId, sections);
        foreach (var section in replacementSections)
        {
            section.Case = entity;
            foreach (var content in section.Items) content.Case = entity;
        }
        db.CaseSections.AddRange(replacementSections);

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return ToTeacherDto(entity);
    }

    public async Task<TeacherCaseDto?> PublishAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null || entity.Archived) return null;
        var itemCount = entity.Sections.Sum(section => section.Items.Count);
        if (itemCount is < 1 or > 100)
            throw new CaseValidationException(new Dictionary<string, string[]> { ["sections"] = ["A published case must contain between 1 and 100 content items."] });
        entity.IsPublished = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToTeacherDto(entity);
    }

    public async Task<bool> ArchiveAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null) return false;
        entity.Archived = true;
        entity.IsPublished = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var link in entity.ShareLinks.Where(link => link.RevokedAt is null)) link.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await db.TeacherCases.FirstOrDefaultAsync(item => item.Id == caseId && item.OwnerId == ownerId, cancellationToken);
        if (entity is null) return false;
        entity.Archived = false;
        entity.IsPublished = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static StudentCaseDto ToStudentDto(TeacherCase entity, string shareToken, MarkdownRenderer renderer)
    {
        var orderedItems = entity.Sections.OrderBy(section => section.Order)
            .SelectMany(section => section.Items.OrderBy(item => item.Order).Select(item => (section, item)))
            .ToArray();
        var steps = orderedItems.Select((entry, index) =>
        {
            var title = entry.item.LevelVersion?.Title ?? entry.item.MarkdownPageVersion?.Title ?? "";
            title = EffectiveTitle(entry.item.DisplayTitle, title);
            if (entry.item.Kind == CaseContentItem.MarkdownKind)
            {
                var version = entry.item.MarkdownPageVersion
                    ?? throw new InvalidOperationException("A Markdown case item is missing its pinned version.");
                var html = renderer.Render(version.Markdown,
                    assetId => $"/api/student/cases/{shareToken}/markdown-assets/{assetId:D}/content");
                return new StudentCaseStepDto(entry.item.Id, CaseContentItem.MarkdownKind, index + 1,
                    entry.section.Order, entry.section.Title, title, null, null, html, false);
            }

            var levelVersion = entry.item.LevelVersion
                ?? throw new InvalidOperationException("A case level is missing its pinned version.");
            var definition = JsonSerializer.Deserialize<LevelDefinition>(levelVersion.DefinitionJson, JsonOptions)
                ?? throw new InvalidOperationException("Level definition snapshot is invalid.");
            definition.Tests = definition.Tests.Where(test => !test.Hidden).ToList();
            return new StudentCaseStepDto(entry.item.Id, CaseContentItem.LevelKind, index + 1,
                entry.section.Order, entry.section.Title, title, levelVersion.Id, definition, null, false);
        }).ToArray();
        var levels = steps.Where(step => step.Kind == CaseContentItem.LevelKind && step.LevelVersionId is not null)
            .Select(step => new StudentCaseLevelDto(step.LevelVersionId!.Value, step.Order, step.SectionOrder, step.SectionTitle,
                step.Title, step.Definition!)).ToArray();
        return new StudentCaseDto(entity.Id, OrderedLevels, entity.Title, entity.ProgressionMode, steps, levels);
    }

    private IQueryable<TeacherCase> OwnedCaseQuery(string ownerId) => db.TeacherCases
        .Include(item => item.Sections).ThenInclude(section => section.Items).ThenInclude(content => content.LevelVersion)
        .Include(item => item.Sections).ThenInclude(section => section.Items).ThenInclude(content => content.MarkdownPageVersion).ThenInclude(version => version!.Page)
        .Include(item => item.ShareLinks)
        .Where(item => item.OwnerId == ownerId);

    private async Task<List<ValidatedCaseSection>> ValidateAndLoadSectionsAsync(
        string ownerId,
        string caseType,
        string title,
        string description,
        IReadOnlyList<CaseSectionInput> sections,
        CaseProgressionMode progressionMode,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (caseType != OrderedLevels) errors["caseType"] = ["Only orderedLevels cases are currently supported."];
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) errors["title"] = ["Title must contain between 1 and 200 characters."];
        if (description is null || description.Trim().Length > 2000) errors["description"] = ["Description cannot exceed 2000 characters."];
        if (!Enum.IsDefined(progressionMode)) errors["progressionMode"] = ["Choose Sequential or AllOpen progression."];
        if (sections is null || sections.Count == 0 || sections.Any(section => section is null))
            errors["sections"] = ["A case must contain at least one section."];

        var safeSections = sections?.Where(section => section is not null).ToArray() ?? [];
        foreach (var section in safeSections)
        {
            if (section.Items is null)
            {
                errors["sections"] = ["Each section must contain an item list."];
                continue;
            }
            if (section.Title?.Trim().Length is > 120)
                errors["sections"] = ["Section names cannot exceed 120 characters."];
            if (section.Items.Any(content => content is null))
                errors["sections"] = ["A section contains an invalid content item."];
            foreach (var content in section.Items.Where(content => content is not null))
            {
                var kind = NormalizeKind(content.Kind);
                if (kind == CaseContentItem.LevelKind && (content.LevelVersionId is null || content.MarkdownPageVersionId is not null)
                    || kind == CaseContentItem.MarkdownKind && (content.MarkdownPageVersionId is null || content.LevelVersionId is not null)
                    || kind is not (CaseContentItem.LevelKind or CaseContentItem.MarkdownKind))
                    errors["sections"] = ["Each content item must reference exactly one supported content version."];
                if (content.DisplayTitle?.Trim().Length is > 200)
                    errors["sections"] = ["Custom content names cannot exceed 200 characters."];
            }
        }

        var inputs = safeSections.SelectMany(section => section.Items is null
            ? Enumerable.Empty<CaseContentItemInput>()
            : section.Items.Where(content => content is not null)).ToArray();
        if (inputs.Length > 100)
            errors["sections"] = ["A case cannot contain more than 100 content items."];
        var levelVersionIds = inputs.Where(item => NormalizeKind(item.Kind) == CaseContentItem.LevelKind && item.LevelVersionId is not null)
            .Select(item => item.LevelVersionId!.Value).ToArray();
        if (levelVersionIds.Distinct().Count() != levelVersionIds.Length)
            errors["sections"] = ["A level version can only appear once across all sections."];
        if (errors.Count != 0) throw new CaseValidationException(errors);

        var pageVersionIds = inputs.Where(item => NormalizeKind(item.Kind) == CaseContentItem.MarkdownKind)
            .Select(item => item.MarkdownPageVersionId!.Value).Distinct().ToArray();
        var levelVersions = await db.LevelVersions.Include(version => version.Level)
            .Where(version => levelVersionIds.Contains(version.Id) && version.Level.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        var pageVersions = await db.MarkdownPageVersions.Include(version => version.Page)
            .Where(version => pageVersionIds.Contains(version.Id) && version.Page.OwnerId == ownerId && !version.Page.IsArchived)
            .ToListAsync(cancellationToken);
        if (levelVersions.Count != levelVersionIds.Length || pageVersions.Count != pageVersionIds.Length)
            throw new CaseValidationException(new Dictionary<string, string[]>
            {
                ["sections"] = ["One or more selected level or Markdown page versions are unavailable to this set owner."]
            });

        var levelsById = levelVersions.ToDictionary(version => version.Id);
        var pagesById = pageVersions.ToDictionary(version => version.Id);
        return safeSections.Select(section => new ValidatedCaseSection(
            NormalizeOptional(section.Title),
            section.Items!.Where(content => content is not null).Select(content =>
            {
                var kind = NormalizeKind(content.Kind);
                return kind == CaseContentItem.LevelKind
                    ? new ValidatedCaseContentItem(kind, levelsById[content.LevelVersionId!.Value], null, NormalizeOptional(content.DisplayTitle))
                    : new ValidatedCaseContentItem(kind, null, pagesById[content.MarkdownPageVersionId!.Value], NormalizeOptional(content.DisplayTitle));
            }).ToArray())).ToList();
    }

    private static List<CaseSection> BuildSections(Guid caseId, IReadOnlyList<ValidatedCaseSection> sections)
        => sections.Select((section, sectionOrder) =>
        {
            var entity = new CaseSection
            {
                CaseId = caseId,
                Title = section.Title,
                Order = sectionOrder
            };
            entity.Items = section.Items.Select((item, itemOrder) => new CaseContentItem
            {
                CaseId = caseId,
                SectionId = entity.Id,
                Section = entity,
                Kind = item.Kind,
                LevelVersionId = item.LevelVersion?.Id,
                LevelVersion = item.LevelVersion,
                MarkdownPageVersionId = item.MarkdownPageVersion?.Id,
                MarkdownPageVersion = item.MarkdownPageVersion,
                Order = itemOrder,
                DisplayTitle = item.DisplayTitle
            }).ToList();
            return entity;
        }).ToList();

    private static string EffectiveTitle(string? displayTitle, string canonicalTitle)
        => string.IsNullOrWhiteSpace(displayTitle) ? canonicalTitle : displayTitle;

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string NormalizeKind(string? kind) => kind?.Trim().ToLowerInvariant() ?? "";

    private static TeacherCaseDto ToTeacherDto(TeacherCase item)
    {
        var sections = item.Sections.OrderBy(section => section.Order).Select(section => new CaseSectionDto(
            section.Id,
            section.Title,
            section.Order,
            section.Items.OrderBy(content => content.Order).Select(content => new CaseContentItemDto(
                content.Id,
                content.Kind,
                content.LevelVersionId,
                content.MarkdownPageVersionId,
                content.Order,
                content.LevelVersion?.Title ?? content.MarkdownPageVersion?.Title ?? "",
                content.DisplayTitle,
                content.MarkdownPageVersion?.Page.IsBuiltIn ?? false)).ToArray())).ToArray();
        return new TeacherCaseDto(
            item.Id,
            item.CaseType,
            item.Title,
            item.Description,
            item.IsPublished,
            item.Archived,
            item.ProgressionMode,
            sections,
            sections.Sum(section => section.Items.Count),
            item.ShareLinks.Any(link => link.RevokedAt is null));
    }

    private sealed record ValidatedCaseSection(string? Title, IReadOnlyList<ValidatedCaseContentItem> Items);
    private sealed record ValidatedCaseContentItem(string Kind, LevelVersionEntity? LevelVersion, MarkdownPageVersion? MarkdownPageVersion, string? DisplayTitle);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class CaseValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Case is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
