using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Data;

namespace Synapse.Blocks.Api.Auth;

public sealed class InvitationService(AppDbContext db, UserManager<ApplicationUser> users)
{
    public async Task<string> CreateAsync(string email, DateTimeOffset expiresAt, string invitedByUserId, CancellationToken cancellationToken = default)
    {
        var token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        db.Invitations.Add(new Invitation
        {
            Email = email.Trim(), TokenHash = Hash(token), InvitedByUserId = invitedByUserId, ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<bool> AcceptAsync(string token, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(password)) return false;
        var hash = Hash(token);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var invitation = await db.Invitations.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (invitation is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(invitation.TokenHash), Convert.FromHexString(hash))
            || invitation.ConsumedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        var user = new ApplicationUser { UserName = invitation.Email, Email = invitation.Email, DisplayName = invitation.Email };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) return false;
        var assigned = await users.AddToRoleAsync(user, BootstrapPlatformAdmin.TeacherRole);
        if (!assigned.Succeeded) throw new InvalidOperationException("Unable to assign teacher role.");
        invitation.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
