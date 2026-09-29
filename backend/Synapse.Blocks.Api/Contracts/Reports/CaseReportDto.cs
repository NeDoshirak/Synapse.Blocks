namespace Synapse.Blocks.Api.Contracts.Reports;

public sealed record CaseReportDto(Guid CaseId, IReadOnlyList<CaseParticipantReportDto> Participants);
public sealed record CaseParticipantReportDto(string DisplayName, int BestProgress, IReadOnlyList<CaseAttemptReportDto> Attempts);
public sealed record CaseAttemptReportDto(Guid AttemptId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int CompletedLevelCount);
