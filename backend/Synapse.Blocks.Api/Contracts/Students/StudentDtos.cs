namespace Synapse.Blocks.Api.Contracts.Students;

public sealed record CreateParticipantRequest(string DisplayName);
public sealed record ParticipantDto(Guid ParticipantId, string DisplayName);
public sealed record AttemptDto(Guid AttemptId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int CompletedLevelCount);
public sealed record PublicTestResultDto(Guid TestId, string Name, bool Passed, string Input, string Expected, string Actual, string Error);
public sealed record LevelEvaluationDto(bool Passed, IReadOnlyList<PublicTestResultDto> PublicTestResults);
