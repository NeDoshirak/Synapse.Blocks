namespace Synapse.Blocks.Api.Contracts.Students;

using Synapse.Blocks.Api.Contracts.Cases;

public sealed record CreateParticipantRequest(string DisplayName);
public sealed record ParticipantDto(Guid ParticipantId, string DisplayName);
public sealed record AttemptDto(Guid AttemptId, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int CompletedLevelCount, IReadOnlyList<Guid> LevelVersionIds, IReadOnlyList<Guid> CompletedLevelVersionIds, IReadOnlyList<StudentCaseLevelDto> Levels);
public sealed record PublicTestResultDto(Guid TestId, string Name, bool Passed, string Input, string Expected, string Actual, string Error);
public sealed record LevelEvaluationDto(bool Passed, IReadOnlyList<PublicTestResultDto> PublicTestResults, IReadOnlyList<Guid> CompletedLevelVersionIds, bool AttemptCompleted);
