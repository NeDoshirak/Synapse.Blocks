namespace Synapse.Blocks.Api.Data.Entities;

public sealed class Participant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public TeacherCase Case { get; set; } = null!;
    public string DisplayName { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string ContinuationHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Attempt> Attempts { get; set; } = [];
}
