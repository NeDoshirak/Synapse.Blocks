namespace Synapse.Blocks.Api.Data.Entities;

public sealed class ShareLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public TeacherCase Case { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}
