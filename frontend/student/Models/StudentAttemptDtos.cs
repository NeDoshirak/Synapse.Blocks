namespace Synapse.Blocks.Models;

public sealed record StudentParticipantDto(Guid ParticipantId, string DisplayName);
public sealed record StudentAttemptDto(Guid AttemptId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int CompletedLevelCount);
public sealed record LevelEvaluationDto(bool Passed, IReadOnlyList<PublicTestResultDto> PublicTestResults);
public sealed record PublicTestResultDto(Guid TestId, string Name, bool Passed, string Input, string Expected, string Actual, string Error);
