using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Levels;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Levels;

public sealed class LevelService(AppDbContext db, LevelDefinitionValidator validator)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<IReadOnlyList<TeacherLevelDto>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        await EnsureStarterLevelsAsync(ownerId, cancellationToken);
        var levels = await db.TeacherLevels.AsNoTracking()
            .Include(level => level.CurrentVersion)
            .Where(level => level.OwnerId == ownerId)
            .OrderBy(level => level.Title)
            .ToListAsync(cancellationToken);
        return levels.Select(ToDto).ToArray();
    }

    public async Task<TeacherLevelDto?> GetAsync(string ownerId, Guid levelId, CancellationToken cancellationToken = default)
    {
        var level = await db.TeacherLevels.AsNoTracking()
            .Include(item => item.CurrentVersion)
            .FirstOrDefaultAsync(item => item.Id == levelId && item.OwnerId == ownerId, cancellationToken);
        return level is null ? null : ToDto(level);
    }

    public async Task<TeacherLevelDto> CreateAsync(string ownerId, LevelDefinition definition, CancellationToken cancellationToken = default)
    {
        Validate(definition);
        definition.Title = definition.Title.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var level = new TeacherLevel { OwnerId = ownerId, Title = definition.Title.Trim() };
        db.TeacherLevels.Add(level);
        await db.SaveChangesAsync(cancellationToken);

        var version = new LevelVersionEntity
        {
            LevelId = level.Id,
            Version = 1,
            Title = level.Title,
            DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions)
        };
        db.LevelVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        level.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        level.CurrentVersion = version;
        return ToDto(level);
    }

    public async Task<TeacherLevelDto?> UpdateAsync(string ownerId, Guid levelId, Guid expectedVersionId, LevelDefinition definition, CancellationToken cancellationToken = default)
    {
        Validate(definition);
        definition.Title = definition.Title.Trim();
        var level = await db.TeacherLevels
            .Include(item => item.CurrentVersion)
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == levelId && item.OwnerId == ownerId, cancellationToken);
        if (level is null) return null;
        if (level.CurrentVersionId != expectedVersionId) throw new LevelVersionConflictException();

        var versionNumber = level.Versions.Count == 0 ? 1 : level.Versions.Max(version => version.Version) + 1;
        var version = new LevelVersionEntity
        {
            LevelId = level.Id,
            Version = versionNumber,
            Title = definition.Title.Trim(),
            DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions)
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.LevelVersions.Add(version);
        level.Title = definition.Title.Trim();
        level.UpdatedAt = DateTimeOffset.UtcNow;
        level.CurrentVersionId = version.Id;
        level.CurrentVersion = version;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new LevelVersionConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            throw new LevelVersionConflictException();
        }
        return ToDto(level);
    }

    public async Task<IReadOnlyList<LevelVersionDto>?> GetVersionsAsync(string ownerId, Guid levelId, CancellationToken cancellationToken = default)
    {
        var owned = await db.TeacherLevels.AnyAsync(level => level.Id == levelId && level.OwnerId == ownerId, cancellationToken);
        if (!owned) return null;
        return await db.LevelVersions.AsNoTracking()
            .Where(version => version.LevelId == levelId)
            .OrderByDescending(version => version.Version)
            .Select(version => new LevelVersionDto(version.Id, version.LevelId, version.Version, version.Title, version.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private void Validate(LevelDefinition definition)
    {
        var errors = validator.Validate(definition);
        if (errors.Count != 0) throw new LevelDefinitionValidationException(errors);
    }

    private async Task EnsureStarterLevelsAsync(string ownerId, CancellationToken cancellationToken)
    {
        var owner = await db.Users.SingleOrDefaultAsync(user => user.Id == ownerId, cancellationToken)
            ?? throw new UnauthorizedAccessException("An authenticated user is required.");
        if (owner.StarterLevelsSeededAt is not null) return;

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [ownerId], cancellationToken);
        await db.Entry(owner).ReloadAsync(cancellationToken);
        if (owner.StarterLevelsSeededAt is not null) return;

        var path = Path.Combine(AppContext.BaseDirectory, "levels.json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var starters = JsonSerializer.Deserialize<List<LevelDefinition>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Starter level catalog could not be read.");
        var levels = new List<TeacherLevel>();
        foreach (var definition in starters)
        {
            definition.Id = Guid.NewGuid();
            var level = new TeacherLevel { OwnerId = ownerId, Title = definition.Title.Trim() };
            var version = new LevelVersionEntity
            {
                Level = level,
                Version = 1,
                Title = level.Title,
                DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions)
            };
            level.Versions.Add(version);
            levels.Add(level);
        }
        db.TeacherLevels.AddRange(levels);
        owner.StarterLevelsSeededAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        foreach (var level in levels) level.CurrentVersionId = level.Versions[0].Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static TeacherLevelDto ToDto(TeacherLevel level)
    {
        var current = level.CurrentVersion ?? throw new InvalidOperationException("Level has no current version.");
        var definition = JsonSerializer.Deserialize<LevelDefinition>(current.DefinitionJson, JsonOptions)
            ?? throw new InvalidOperationException("Level definition snapshot is invalid.");
        return new TeacherLevelDto(level.Id, level.Title, current.Id, definition, current.Version);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class LevelDefinitionValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Level definition is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class LevelVersionConflictException() : Exception("The level was changed by another request.");
