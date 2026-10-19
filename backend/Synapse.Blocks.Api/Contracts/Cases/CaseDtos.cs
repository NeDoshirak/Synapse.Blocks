using Synapse.Blocks.Models;
using Synapse.Blocks.Api.Contracts.Students;

namespace Synapse.Blocks.Api.Contracts.Cases;

public enum CaseProgressionMode
{
    Sequential,
    AllOpen
}

public sealed record CaseContentItemInput(string Kind, Guid? LevelVersionId, Guid? MarkdownPageVersionId, string? DisplayTitle);
public sealed record CaseSectionInput(string? Title, IReadOnlyList<CaseContentItemInput> Items);
public sealed record CaseContentItemDto(Guid Id, string Kind, Guid? LevelVersionId, Guid? MarkdownPageVersionId, int Order, string Title, string? DisplayTitle, bool IsBuiltIn);
public sealed record CaseSectionDto(Guid Id, string? Title, int Order, IReadOnlyList<CaseContentItemDto> Items);
public sealed record TeacherCaseDto(Guid Id, string CaseType, string Title, string Description, bool IsPublished, bool Archived, CaseProgressionMode ProgressionMode, IReadOnlyList<CaseSectionDto> Sections, int ItemCount, bool ShareLinkActive);
public sealed record StudentCaseLevelDto(Guid LevelVersionId, int Order, int? SectionOrder, string? SectionTitle, string DisplayTitle, LevelDefinition Definition);
public sealed record StudentCaseDto(Guid Id, string CaseType, string Title, CaseProgressionMode ProgressionMode, IReadOnlyList<StudentCaseStepDto> Steps, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record ShareLinkCreatedDto(string Token, string Url);
public sealed record CreateCaseRequest(string CaseType, string Title, string Description, IReadOnlyList<CaseSectionInput> Sections, CaseProgressionMode ProgressionMode = CaseProgressionMode.Sequential);
public sealed record UpdateCaseRequest(string CaseType, string Title, string Description, IReadOnlyList<CaseSectionInput> Sections, CaseProgressionMode ProgressionMode = CaseProgressionMode.Sequential);
