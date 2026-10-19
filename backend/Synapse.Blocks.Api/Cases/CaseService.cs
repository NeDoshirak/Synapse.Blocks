using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Cases;

public sealed class CaseService(AppDbContext db)
{
    public const string OrderedLevels = "orderedLevels";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<IReadOnlyList<TeacherCaseDto>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var items = await db.TeacherCases.AsNoTracking()
            .Include(item => item.Sections).ThenInclude(section => section.Levels).ThenInclude(level => level.LevelVersion)
            .Include(item => item.ShareLinks)
            .Where(item => item.OwnerId == ownerId)
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
        var sections = await ValidateAndLoadSectionsAsync(ownerId, request.CaseType, request.Title, request.Description, request.Sections, cancellationToken);
        var entity = new TeacherCase
        {
            OwnerId = ownerId,
            CaseType = OrderedLevels,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Sections = BuildSections(Guid.Empty, sections)
        };

        foreach (var section in entity.Sections)
        {
            section.Case = entity;
            section.CaseId = entity.Id;
            foreach (var level in section.Levels)
            {
                level.Case = entity;
                level.CaseId = entity.Id;
            }
        }

        db.TeacherCases.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToTeacherDto(await OwnedCaseQuery(ownerId).SingleAsync(item => item.Id == entity.Id, cancellationToken));
    }

    public async Task<TeacherCaseDto?> UpdateAsync(string ownerId, Guid caseId, UpdateCaseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null || entity.Archived) return null;

        var sections = await ValidateAndLoadSectionsAsync(ownerId, request.CaseType, request.Title, request.Description, request.Sections, cancellationToken);
        var oldLevels = entity.Sections.SelectMany(section => section.Levels).ToArray();
        db.CaseLevels.RemoveRange(oldLevels);
        db.CaseSections.RemoveRange(entity.Sections);
        entity.Levels.Clear();
        entity.Sections.Clear();

        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.CaseType = OrderedLevels;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var section in BuildSections(caseId, sections))
        {
            section.Case = entity;
            entity.Sections.Add(section);
            foreach (var level in section.Levels) level.Case = entity;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToTeacherDto(entity);
    }

    public async Task<TeacherCaseDto?> PublishAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null || entity.Archived) return null;
        if (entity.Sections.Sum(section => section.Levels.Count) == 0)
            throw new CaseValidationException(new Dictionary<string, string[]> { ["sections"] = ["A case must contain at least one level."] });
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

    internal static StudentCaseDto ToStudentDto(TeacherCase entity)
    {
        var levels = entity.Sections.OrderBy(section => section.Order)
            .SelectMany(section => section.Levels.OrderBy(level => level.Order)
                .Select(level => (section, level)))
            .Select((entry, order) =>
            {
                var definition = JsonSerializer.Deserialize<LevelDefinition>(entry.level.LevelVersion.DefinitionJson, JsonOptions)
                    ?? throw new InvalidOperationException("Level definition snapshot is invalid.");
                definition.Tests = definition.Tests.Where(test => !test.Hidden).ToList();
                return new StudentCaseLevelDto(
                    entry.level.LevelVersionId,
                    order + 1,
                    entry.section.Order,
                    entry.section.Title,
                    EffectiveTitle(entry.level.DisplayTitle, entry.level.LevelVersion.Title),
                    definition);
            }).ToArray();
        return new StudentCaseDto(entity.Id, OrderedLevels, entity.Title, levels);
    }

    private IQueryable<TeacherCase> OwnedCaseQuery(string ownerId) => db.TeacherCases
        .Include(item => item.Sections).ThenInclude(section => section.Levels).ThenInclude(level => level.LevelVersion)
        .Include(item => item.ShareLinks)
        .Where(item => item.OwnerId == ownerId);

    private async Task<List<ValidatedCaseSection>> ValidateAndLoadSectionsAsync(
        string ownerId,
        string caseType,
        string title,
        string description,
        IReadOnlyList<CaseSectionInput> sections,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (caseType != OrderedLevels) errors["caseType"] = ["Only orderedLevels cases are currently supported."];
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) errors["title"] = ["Title must contain between 1 and 200 characters."];
        if (description is null || description.Trim().Length > 2000) errors["description"] = ["Description cannot exceed 2000 characters."];
        if (sections is null || sections.Count == 0 || sections.Any(section => section is null))
            errors["sections"] = ["A case must contain at least one section."];

        var safeSections = sections?.Where(section => section is not null).ToArray() ?? [];
        foreach (var section in safeSections)
        {
            if (section.Levels is null)
            {
                errors["sections"] = ["Each section must contain a level list."];
                continue;
            }
            if (section.Title?.Trim().Length is > 120)
                errors["sections"] = ["Section names cannot exceed 120 characters."];
            if (section.Levels.Any(level => level is null))
                errors["sections"] = ["A section contains an invalid level entry."];
            foreach (var level in section.Levels.Where(level => level is not null))
                if (level.DisplayTitle?.Trim().Length is > 200)
                    errors["sections"] = ["Custom level names cannot exceed 200 characters."];
        }

        var inputs = safeSections.SelectMany(section => section.Levels is null
            ? Enumerable.Empty<CaseLevelInput>()
            : section.Levels.Where(level => level is not null)).ToArray();
        var versionIds = inputs.Select(input => input.LevelVersionId).ToArray();
        if (versionIds.Length is < 1 or > 100)
            errors["sections"] = ["A case must contain between 1 and 100 levels."];
        else if (versionIds.Distinct().Count() != versionIds.Length)
            errors["sections"] = ["A level version can only appear once across all sections."];
        if (errors.Count != 0) throw new CaseValidationException(errors);

        var versions = await db.LevelVersions.Include(version => version.Level)
            .Where(version => versionIds.Contains(version.Id) && version.Level.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        if (versions.Count != versionIds.Length)
            throw new CaseValidationException(new Dictionary<string, string[]> { ["sections"] = ["One or more selected level versions are unavailable."] });

        var byId = versions.ToDictionary(version => version.Id);
        return safeSections.Select(section => new ValidatedCaseSection(
            NormalizeOptional(section.Title),
            section.Levels!.Where(level => level is not null).Select(level => new ValidatedCaseLevel(
                byId[level.LevelVersionId],
                NormalizeOptional(level.DisplayTitle))).ToArray())).ToList();
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
            entity.Levels = section.Levels.Select((item, levelOrder) => new CaseLevel
            {
                CaseId = caseId,
                SectionId = entity.Id,
                Section = entity,
                LevelId = item.Version.LevelId,
                LevelVersionId = item.Version.Id,
                LevelVersion = item.Version,
                Order = levelOrder,
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

    private static TeacherCaseDto ToTeacherDto(TeacherCase item)
    {
        var sections = item.Sections.OrderBy(section => section.Order).Select(section => new CaseSectionDto(
            section.Id,
            section.Title,
            section.Order,
            section.Levels.OrderBy(level => level.Order).Select(level => new CaseLevelDto(
                level.LevelId,
                level.LevelVersionId,
                level.Order,
                level.LevelVersion.Title,
                level.DisplayTitle)).ToArray())).ToArray();
        return new TeacherCaseDto(
            item.Id,
            item.CaseType,
            item.Title,
            item.Description,
            item.IsPublished,
            item.Archived,
            sections,
            sections.Sum(section => section.Levels.Count),
            item.ShareLinks.Any(link => link.RevokedAt is null));
    }

    private sealed record ValidatedCaseSection(string? Title, IReadOnlyList<ValidatedCaseLevel> Levels);
    private sealed record ValidatedCaseLevel(LevelVersionEntity Version, string? DisplayTitle);

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
