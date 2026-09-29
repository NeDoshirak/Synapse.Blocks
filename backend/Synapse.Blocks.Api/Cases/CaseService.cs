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
        var items = await db.TeacherCases.AsNoTracking().Include(item => item.Levels).ThenInclude(level => level.LevelVersion)
            .Include(item => item.ShareLinks).Where(item => item.OwnerId == ownerId)
            .OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToTeacherDto).ToArray();
    }

    public async Task<TeacherCaseDto?> GetAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var item = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        return item is null ? null : ToTeacherDto(item);
    }

    public async Task<TeacherCaseDto> CreateAsync(string ownerId, CreateCaseRequest request, CancellationToken cancellationToken = default)
    {
        var levels = await ValidateAndLoadVersionsAsync(ownerId, request.CaseType, request.Title, request.Description, request.OrderedLevelVersionIds, cancellationToken);
        var entity = new TeacherCase
        {
            OwnerId = ownerId,
            CaseType = OrderedLevels,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Levels = levels.Select((version, order) => new CaseLevel { LevelId = version.LevelId, LevelVersionId = version.Id, Order = order }).ToList()
        };
        db.TeacherCases.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToTeacherDto(await OwnedCaseQuery(ownerId).SingleAsync(item => item.Id == entity.Id, cancellationToken));
    }

    public async Task<TeacherCaseDto?> UpdateAsync(string ownerId, Guid caseId, UpdateCaseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null) return null;
        if (entity.Archived) return null;
        var levels = await ValidateAndLoadVersionsAsync(ownerId, request.CaseType, request.Title, request.Description, request.OrderedLevelVersionIds, cancellationToken);
        db.CaseLevels.RemoveRange(entity.Levels);
        entity.Levels.Clear();
        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.CaseType = OrderedLevels;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var (version, order) in levels.Select((version, order) => (version, order)))
            entity.Levels.Add(new CaseLevel { CaseId = caseId, LevelId = version.LevelId, LevelVersionId = version.Id, Order = order });
        await db.SaveChangesAsync(cancellationToken);
        return ToTeacherDto(entity);
    }

    public async Task<TeacherCaseDto?> PublishAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await OwnedCaseQuery(ownerId).FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        if (entity is null || entity.Archived) return null;
        if (entity.Levels.Count == 0) throw new CaseValidationException(new Dictionary<string, string[]> { ["orderedLevelVersionIds"] = ["A case must contain at least one level."] });
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
        var levels = entity.Levels.OrderBy(level => level.Order).Select(level =>
        {
            var definition = JsonSerializer.Deserialize<LevelDefinition>(level.LevelVersion.DefinitionJson, JsonOptions)
                ?? throw new InvalidOperationException("Level definition snapshot is invalid.");
            definition.Tests = definition.Tests.Where(test => !test.Hidden).ToList();
            return new StudentCaseLevelDto(level.LevelVersionId, level.Order, definition);
        }).ToArray();
        return new StudentCaseDto(entity.Id, OrderedLevels, entity.Title, levels);
    }

    private IQueryable<TeacherCase> OwnedCaseQuery(string ownerId) => db.TeacherCases
        .Include(item => item.Levels).ThenInclude(level => level.LevelVersion)
        .Include(item => item.ShareLinks)
        .Where(item => item.OwnerId == ownerId);

    private async Task<List<LevelVersionEntity>> ValidateAndLoadVersionsAsync(
        string ownerId,
        string caseType,
        string title,
        string description,
        IReadOnlyList<Guid> versionIds,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (caseType != OrderedLevels) errors["caseType"] = ["Only orderedLevels cases are currently supported."];
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) errors["title"] = ["Title must contain between 1 and 200 characters."];
        if (description is null || description.Trim().Length > 2000) errors["description"] = ["Description cannot exceed 2000 characters."];
        if (versionIds is null || versionIds.Count is < 1 or > 100) errors["orderedLevelVersionIds"] = ["A case must contain between 1 and 100 levels."];
        else if (versionIds.Distinct().Count() != versionIds.Count) errors["orderedLevelVersionIds"] = ["A level version can only appear once."];
        if (errors.Count != 0) throw new CaseValidationException(errors);

        var requestedIds = versionIds!;
        var versions = await db.LevelVersions.Include(version => version.Level)
            .Where(version => requestedIds.Contains(version.Id) && version.Level.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        if (versions.Count != requestedIds.Count)
            throw new CaseValidationException(new Dictionary<string, string[]> { ["orderedLevelVersionIds"] = ["One or more selected level versions are unavailable."] });
        var byId = versions.ToDictionary(version => version.Id);
        return requestedIds.Select(id => byId[id]).ToList();
    }

    private static TeacherCaseDto ToTeacherDto(TeacherCase item) => new(
        item.Id,
        item.CaseType,
        item.Title,
        item.Description,
        item.IsPublished,
        item.Archived,
        item.Levels.OrderBy(level => level.Order).Select(level => new CaseLevelDto(level.LevelId, level.LevelVersionId, level.Order, level.LevelVersion.Title)).ToArray(),
        item.ShareLinks.Any(link => link.RevokedAt is null));

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
