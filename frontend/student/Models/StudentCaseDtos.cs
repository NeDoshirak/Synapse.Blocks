using Synapse.Blocks.Models;

namespace Synapse.Blocks.Models;

public sealed record StudentCaseDto(Guid Id, string CaseType, string Title, string ProgressionMode, IReadOnlyList<StudentCaseStepDto> Steps, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record StudentCaseStepDto(Guid StepId, string Kind, int Order, int? SectionOrder, string? SectionTitle, string Title, Guid? LevelVersionId, LevelDefinition? Definition, string? MarkdownHtml, bool IsCompleted);
public sealed record StudentCaseLevelDto(Guid LevelVersionId, int Order, int? SectionOrder, string? SectionTitle, string DisplayTitle, LevelDefinition Definition);
