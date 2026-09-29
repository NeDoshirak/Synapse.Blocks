namespace Synapse.Blocks.Api.Data.Entities;

public sealed class AttemptLevelResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public Attempt Attempt { get; set; } = null!;
    public Guid LevelVersionId { get; set; }
    public LevelVersionEntity LevelVersion { get; set; } = null!;
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastSubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}
