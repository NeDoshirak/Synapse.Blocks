namespace Synapse.Blocks.Api.Data.Entities;

public sealed class CaseLevel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public TeacherCase Case { get; set; } = null!;
    public Guid LevelId { get; set; }
    public Guid LevelVersionId { get; set; }
    public LevelVersionEntity LevelVersion { get; set; } = null!;
    public int Order { get; set; }
}
