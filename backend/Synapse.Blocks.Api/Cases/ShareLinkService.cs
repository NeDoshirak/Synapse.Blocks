using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.Cases;

public sealed class ShareLinkService(AppDbContext db, IConfiguration configuration)
{
    public async Task<ShareLinkCreatedDto?> CreateOrRotateAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
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
        await transaction.CommitAsync(cancellationToken);
        var origin = (configuration["PublicOrigin"] ?? "http://localhost:8080").TrimEnd('/');
        return new ShareLinkCreatedDto(token, $"{origin}/case/{token}");
    }

    public async Task<bool> RevokeAsync(string ownerId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var item = await db.TeacherCases.Include(entity => entity.ShareLinks)
            .FirstOrDefaultAsync(entity => entity.Id == caseId && entity.OwnerId == ownerId, cancellationToken);
        if (item is null) return false;
        foreach (var link in item.ShareLinks.Where(link => link.RevokedAt is null)) link.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<StudentCaseDto?> ResolveAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43) return null;
        var hash = Hash(token);
        var link = await db.ShareLinks.AsNoTracking()
            .Include(item => item.Case).ThenInclude(item => item.Levels).ThenInclude(level => level.LevelVersion)
            .FirstOrDefaultAsync(item => item.TokenHash == hash && item.RevokedAt == null, cancellationToken);
        if (link is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(link.TokenHash), Convert.FromHexString(hash))
            || !link.Case.IsPublished || link.Case.Archived)
            return null;
        return CaseService.ToStudentDto(link.Case);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
