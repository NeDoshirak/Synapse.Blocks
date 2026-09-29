namespace Synapse.Blocks.Models;

public sealed record StudentParticipantDto(Guid ParticipantId, string DisplayName);
public sealed record CreateStudentParticipantRequest(string DisplayName);
public sealed record EmptyStudentRequest();
public sealed record StudentAttemptDto(Guid AttemptId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int CompletedLevelCount, IReadOnlyList<Guid> LevelVersionIds, IReadOnlyList<Guid> CompletedLevelVersionIds, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record LevelEvaluationDto(bool Passed, IReadOnlyList<PublicTestResultDto> PublicTestResults, IReadOnlyList<Guid> CompletedLevelVersionIds, bool AttemptCompleted);
public sealed record PublicTestResultDto(Guid TestId, string Name, bool Passed, string Input, string Expected, string Actual, string Error);
