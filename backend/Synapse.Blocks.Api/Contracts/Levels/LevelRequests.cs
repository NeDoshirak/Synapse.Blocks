using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Contracts.Levels;

public sealed record CreateLevelRequest(LevelDefinition Definition);
public sealed record UpdateLevelRequest(Guid ExpectedVersionId, LevelDefinition Definition);
