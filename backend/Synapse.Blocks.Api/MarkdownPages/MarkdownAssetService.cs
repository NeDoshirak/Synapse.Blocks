using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.MarkdownPages;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.MarkdownPages;

public sealed class MarkdownAssetService(AppDbContext db)
{
    public const int MaximumAssetBytes = 10 * 1024 * 1024;

    public async Task<MarkdownAssetDto> UploadAsync(
        string ownerId,
        Stream stream,
        string declaredContentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId) ||
            !await db.Users.OfType<ApplicationUser>().AnyAsync(user => user.Id == ownerId && user.IsActive, cancellationToken))
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["owner"] = ["The page owner is unavailable."] });

        await using var content = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (content.Length + read > MaximumAssetBytes)
                throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["file"] = [$"Images must be no larger than {MaximumAssetBytes} bytes."] });
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var bytes = content.ToArray();
        var detectedType = DetectContentType(bytes);
        if (detectedType is null || !string.Equals(detectedType, declaredContentType?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["file"] = ["Only valid PNG, JPEG, and WebP images are accepted; the detected type must match the declared type."] });

        var asset = new MarkdownAsset
        {
            OwnerId = ownerId,
            ContentType = detectedType,
            Length = bytes.LongLength,
            Content = bytes,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.MarkdownAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(asset);
    }

    public async Task<MarkdownAssetContent?> GetForPreviewAsync(string ownerId, Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await db.MarkdownAssets.AsNoTracking()
            .Where(item => item.Id == assetId && item.OwnerId == ownerId && item.Owner.IsActive)
            .Select(item => new MarkdownAssetContent(item.ContentType, item.Content, item.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return asset;
    }

    public async Task<bool> OwnsAllReferencedAssetsAsync(string ownerId, string markdown, CancellationToken cancellationToken = default)
    {
        var ids = System.Text.RegularExpressions.Regex.Matches(markdown ?? "", @"asset:(?<id>[0-9a-fA-F-]{36})")
            .Select(match => Guid.TryParse(match.Groups["id"].Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0) return true;

        var ownedCount = await db.MarkdownAssets.CountAsync(asset => ids.Contains(asset.Id) && asset.OwnerId == ownerId, cancellationToken);
        return ownedCount == ids.Length;
    }

    public static string PreviewUrl(Guid assetId) => $"/api/teacher/markdown-assets/{assetId:D}/content";

    private static MarkdownAssetDto ToDto(MarkdownAsset asset) =>
        new(asset.Id, asset.ContentType, asset.Length, PreviewUrl(asset.Id));

    private static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.StartsWith(png)) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
