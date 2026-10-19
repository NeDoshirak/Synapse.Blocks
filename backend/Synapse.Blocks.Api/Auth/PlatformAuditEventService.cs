using Synapse.Blocks.Api.Data;

namespace Synapse.Blocks.Api.Auth;

public sealed class PlatformAuditEventService(AppDbContext db)
{
    public async Task RecordAsync(
        string actorUserId,
        string action,
        string targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
