using Synapse.Blocks.Models;

namespace Synapse.Blocks.Models;

public sealed record StudentCaseDto(Guid Id, string CaseType, string Title, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record StudentCaseLevelDto(Guid LevelVersionId, int Order, LevelDefinition Definition);
