namespace Synapse.Blocks.Api.Contracts.Cases;

public sealed record PlatformCaseDto(
    Guid Id,
    string OwnerId,
    string OwnerName,
    string OwnerEmail,
    string CaseType,
    string Title,
    string Description,
    bool IsPublished,
    bool Archived,
    CaseProgressionMode ProgressionMode,
    IReadOnlyList<CaseSectionDto> Sections,
    int ItemCount,
    bool ShareLinkActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlatformCreateCaseRequest(
    string OwnerId,
    string CaseType,
    string Title,
    string Description,
    IReadOnlyList<CaseSectionInput> Sections,
    CaseProgressionMode ProgressionMode = CaseProgressionMode.Sequential);
