using Synapse.Blocks.Api.Contracts.Cases;

namespace Synapse.Blocks.Api.Data.Entities;

public sealed class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public TeacherCase Case { get; set; } = null!;
    public Guid ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public string Status { get; set; } = "InProgress";
    public CaseProgressionMode ProgressionMode { get; set; } = CaseProgressionMode.Sequential;
    public string LevelVersionIdsJson { get; set; } = "[]";
    public string LevelPresentationJson { get; set; } = "[]";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<AttemptLevelResult> LevelResults { get; set; } = [];
    public List<AttemptStep> Steps { get; set; } = [];
}
