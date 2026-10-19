using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Auth;
using Synapse.Blocks.Api.Data;

namespace Synapse.Blocks.Api.Auth;

public sealed class TeacherAccountService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    InvitationService invitations,
    PlatformAuditEventService audit,
    IConfiguration configuration)
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromDays(1);

    public async Task<IReadOnlyList<TeacherAccountDto>> ListTeachersAsync(string? search, CancellationToken cancellationToken = default)
    {
        var roleId = await db.Roles.Where(role => role.Name == BootstrapPlatformAdmin.TeacherRole)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (roleId is null) return [];

        var query = from user in db.Users.AsNoTracking()
                    join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                    where userRole.RoleId == roleId
                    select user;

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var normalized = term.ToUpperInvariant();
            query = query.Where(user =>
                (user.NormalizedEmail ?? "").Contains(normalized)
                || (user.FirstName ?? "").ToUpper().Contains(normalized)
                || (user.LastName ?? "").ToUpper().Contains(normalized)
                || user.DisplayName.ToUpper().Contains(normalized));
        }

        var accounts = await query
            .OrderBy(user => user.LastName ?? user.DisplayName)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.Email)
            .ToListAsync(cancellationToken);

        return accounts.Select(user => new TeacherAccountDto(
            user.Id,
            user.Email ?? user.UserName ?? "",
            DisplayName(user),
            user.FirstName,
            user.LastName,
            user.TeacherDescription,
            user.IsActive,
            user.TwoFactorEnabled)).ToArray();
    }

    public async Task<IReadOnlyList<PendingInvitationDto>> ListPendingInvitationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Invitations.AsNoTracking()
            .Where(invitation => invitation.ConsumedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now)
            .OrderBy(invitation => invitation.ExpiresAt)
            .Select(invitation => new PendingInvitationDto(invitation.Id, invitation.Email, invitation.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvitationCreatedDto?> CreateInvitationAsync(
        string email,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var normalizedEmail = users.NormalizeEmail(email.Trim());
        if (string.IsNullOrEmpty(normalizedEmail) || await users.FindByEmailAsync(email.Trim()) is not null)
            return null;

        var now = DateTimeOffset.UtcNow;
        var existingInvite = await db.Invitations.AnyAsync(invitation =>
            invitation.Email.ToUpper() == normalizedEmail
            && invitation.ConsumedAt == null
            && invitation.RevokedAt == null
            && invitation.ExpiresAt > now,
            cancellationToken);
        if (existingInvite) return null;

        var expiresAt = now.Add(InvitationLifetime);
        var token = await invitations.CreateAsync(email, expiresAt, actorUserId, cancellationToken);
        await audit.RecordAsync(actorUserId, "teacher.invitation.created", "invitation", email.Trim(), cancellationToken);

        var origin = (configuration["PublicOrigin"] ?? "http://localhost:8080").TrimEnd('/');
        var url = $"{origin}/admin/invitations/accept?token={Uri.EscapeDataString(token)}";
        await transaction.CommitAsync(cancellationToken);
        return new InvitationCreatedDto(url, expiresAt);
    }

    public async Task<bool> RevokeInvitationAsync(Guid invitationId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var invitation = await db.Invitations.SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation is null || invitation.ConsumedAt is not null || invitation.RevokedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        invitation.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(actorUserId, "teacher.invitation.revoked", "invitation", invitation.Id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetTeacherActiveAsync(
        string teacherId,
        bool isActive,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var teacher = await FindTeacherAsync(teacherId);
        if (teacher is null) return false;
        if (teacher.IsActive == isActive) return true;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        teacher.IsActive = isActive;
        var updated = await users.UpdateAsync(teacher);
        if (!updated.Succeeded) throw new InvalidOperationException("Could not update the teacher account status.");
        var stamp = await users.UpdateSecurityStampAsync(teacher);
        if (!stamp.Succeeded) throw new InvalidOperationException("Could not invalidate teacher sessions.");

        var action = isActive ? "teacher.reactivated" : "teacher.blocked";
        await audit.RecordAsync(actorUserId, action, "teacher", teacher.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PasswordResetLinkDto?> CreatePasswordResetLinkAsync(
        string teacherId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var teacher = await FindTeacherAsync(teacherId);
        if (teacher is null || string.IsNullOrEmpty(teacher.Email)) return null;

        var token = await users.GeneratePasswordResetTokenAsync(teacher);
        var origin = (configuration["PublicOrigin"] ?? "http://localhost:8080").TrimEnd('/');
        var query = $"email={Uri.EscapeDataString(teacher.Email)}&token={Uri.EscapeDataString(token)}";
        var url = $"{origin}/admin/password-reset?{query}";
        var expiresAt = DateTimeOffset.UtcNow.Add(PasswordResetLifetime);
        await audit.RecordAsync(actorUserId, "teacher.password-reset.created", "teacher", teacher.Id, cancellationToken);
        return new PasswordResetLinkDto(url, expiresAt);
    }

    public async Task<bool> ResetTeacherTwoFactorAsync(
        string teacherId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var teacher = await FindTeacherAsync(teacherId);
        if (teacher is null) return false;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var disabled = await users.SetTwoFactorEnabledAsync(teacher, false);
        if (!disabled.Succeeded) throw new InvalidOperationException("Could not disable the teacher authenticator.");
        var resetKey = await users.ResetAuthenticatorKeyAsync(teacher);
        if (!resetKey.Succeeded) throw new InvalidOperationException("Could not reset the teacher authenticator key.");
        _ = await users.GenerateNewTwoFactorRecoveryCodesAsync(teacher, 0);
        var stamp = await users.UpdateSecurityStampAsync(teacher);
        if (!stamp.Succeeded) throw new InvalidOperationException("Could not invalidate teacher sessions.");

        await audit.RecordAsync(actorUserId, "teacher.two-factor.reset", "teacher", teacher.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<ApplicationUser?> FindTeacherAsync(string teacherId)
    {
        var teacher = await users.FindByIdAsync(teacherId);
        return teacher is not null && await users.IsInRoleAsync(teacher, BootstrapPlatformAdmin.TeacherRole)
            ? teacher
            : null;
    }

    private static string DisplayName(ApplicationUser user)
    {
        var profileName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(profileName)) return profileName;
        if (!string.IsNullOrWhiteSpace(user.DisplayName)) return user.DisplayName;
        return user.Email ?? "Преподаватель";
    }
}
