using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Contracts.Levels;

public sealed record TeacherLevelDto(Guid Id, string Title, Guid CurrentVersionId, LevelDefinition Definition, int Version);
public sealed record LevelVersionDto(Guid Id, Guid LevelId, int Version, string Title, DateTimeOffset CreatedAt);
