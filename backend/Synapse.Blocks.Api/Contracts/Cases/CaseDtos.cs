using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Contracts.Cases;

public sealed record CaseLevelDto(Guid LevelId, Guid LevelVersionId, int Order, string Title);
public sealed record TeacherCaseDto(Guid Id, string CaseType, string Title, string Description, bool IsPublished, bool Archived, IReadOnlyList<CaseLevelDto> Levels, bool ShareLinkActive);
public sealed record StudentCaseLevelDto(Guid LevelVersionId, int Order, LevelDefinition Definition);
public sealed record StudentCaseDto(Guid Id, string CaseType, string Title, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record ShareLinkCreatedDto(string Token, string Url);
public sealed record CreateCaseRequest(string CaseType, string Title, string Description, IReadOnlyList<Guid> OrderedLevelVersionIds);
public sealed record UpdateCaseRequest(string CaseType, string Title, string Description, IReadOnlyList<Guid> OrderedLevelVersionIds);
