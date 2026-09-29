namespace Synapse.Blocks.Api.Auth;

public sealed class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string InvitedByUserId { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
