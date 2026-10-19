using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Contracts.Cases;

public sealed record CaseLevelInput(Guid LevelVersionId, string? DisplayTitle);
public sealed record CaseSectionInput(string? Title, IReadOnlyList<CaseLevelInput> Levels);
public sealed record CaseLevelDto(Guid LevelId, Guid LevelVersionId, int Order, string Title, string? DisplayTitle);
public sealed record CaseSectionDto(Guid Id, string? Title, int Order, IReadOnlyList<CaseLevelDto> Levels);
public sealed record TeacherCaseDto(Guid Id, string CaseType, string Title, string Description, bool IsPublished, bool Archived, IReadOnlyList<CaseSectionDto> Sections, int LevelCount, bool ShareLinkActive);
public sealed record StudentCaseLevelDto(Guid LevelVersionId, int Order, int? SectionOrder, string? SectionTitle, string DisplayTitle, LevelDefinition Definition);
public sealed record StudentCaseDto(Guid Id, string CaseType, string Title, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record ShareLinkCreatedDto(string Token, string Url);
public sealed record CreateCaseRequest(string CaseType, string Title, string Description, IReadOnlyList<CaseSectionInput> Sections);
public sealed record UpdateCaseRequest(string CaseType, string Title, string Description, IReadOnlyList<CaseSectionInput> Sections);
