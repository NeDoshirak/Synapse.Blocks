using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.Cases;

public sealed class ShareLinkService(AppDbContext db, IConfiguration configuration, PlatformAuditEventService audit)
{
    public async Task<ShareLinkCreatedDto?> CreateOrRotateAsync(
        string ownerId,
        Guid caseId,
        CancellationToken cancellationToken = default,
        string? auditActorId = null)
    {
        var item = await db.TeacherCases.Include(entity => entity.ShareLinks)
            .FirstOrDefaultAsync(entity => entity.Id == caseId && entity.OwnerId == ownerId && entity.IsPublished && !entity.Archived, cancellationToken);
        if (item is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [caseId.ToString()], cancellationToken);
        foreach (var oldLink in item.ShareLinks.Where(link => link.RevokedAt is null)) oldLink.RevokedAt = DateTimeOffset.UtcNow;

        var token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        db.ShareLinks.Add(new ShareLink { CaseId = caseId, TokenHash = Hash(token) });
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(auditActorId))
            await audit.RecordAsync(auditActorId, "case.share-link.rotated.by-platform-admin", "case", caseId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var origin = (configuration["PublicOrigin"] ?? "http://localhost:8080").TrimEnd('/');
        return new ShareLinkCreatedDto(token, $"{origin}/case/{token}");
    }

    public async Task<bool> RevokeAsync(
        string ownerId,
        Guid caseId,
        CancellationToken cancellationToken = default,
        string? auditActorId = null)
    {
        var item = await db.TeacherCases.Include(entity => entity.ShareLinks)
            .FirstOrDefaultAsync(entity => entity.Id == caseId && entity.OwnerId == ownerId, cancellationToken);
        if (item is null) return false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var link in item.ShareLinks.Where(link => link.RevokedAt is null)) link.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(auditActorId))
            await audit.RecordAsync(auditActorId, "case.share-link.revoked.by-platform-admin", "case", caseId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<StudentCaseDto?> ResolveAsync(string token, CancellationToken cancellationToken = default)
    {
        var item = await ResolveCaseAsync(token, cancellationToken);
        return item is null ? null : CaseService.ToStudentDto(item);
    }

    public async Task<TeacherCase?> ResolveCaseAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43) return null;
        var hash = Hash(token);
        var link = await db.ShareLinks.AsNoTracking()
            .Include(item => item.Case).ThenInclude(item => item.Sections).ThenInclude(section => section.Levels).ThenInclude(level => level.LevelVersion)
            .FirstOrDefaultAsync(item => item.TokenHash == hash && item.RevokedAt == null, cancellationToken);
        if (link is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(link.TokenHash), Convert.FromHexString(hash))
            || !link.Case.IsPublished || link.Case.Archived)
            return null;
        return link.Case;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
