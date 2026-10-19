namespace Synapse.Blocks.Api.Auth;

public sealed class PlatformAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActorUserId { get; set; } = "";
    public ApplicationUser Actor { get; set; } = null!;
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string TargetId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
