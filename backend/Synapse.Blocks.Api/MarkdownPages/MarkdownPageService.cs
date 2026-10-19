using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.MarkdownPages;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.MarkdownPages;

public sealed class MarkdownPageService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    MarkdownAssetService assets,
    MarkdownRenderer renderer)
{
    public async Task<IReadOnlyList<MarkdownPageDto>> ListAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveTeacherAsync(ownerId)) return [];
        return await db.MarkdownPages.AsNoTracking()
            .Where(page => page.OwnerId == ownerId)
            .OrderBy(page => page.IsArchived).ThenBy(page => page.Title)
            .Select(page => new MarkdownPageDto(page.Id, page.Title, page.CurrentVersionId ?? Guid.Empty,
                page.Versions.Where(version => version.Id == page.CurrentVersionId).Select(version => version.Version).FirstOrDefault(),
                page.IsBuiltIn, page.IsArchived))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarkdownPageDto>?> ListForPlatformAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveTeacherAsync(ownerId)) return null;
        return await ListAsync(ownerId, cancellationToken);
    }

    public async Task<MarkdownPageDetailDto?> GetAsync(string ownerId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (!await IsActiveTeacherAsync(ownerId)) return null;
        var page = await db.MarkdownPages.AsNoTracking()
            .Include(item => item.CurrentVersion)
            .SingleOrDefaultAsync(item => item.Id == pageId && item.OwnerId == ownerId, cancellationToken);
        return page?.CurrentVersion is null ? null : ToDetail(page, page.CurrentVersion);
    }

    public async Task<MarkdownPageDetailDto> CreateAsync(string ownerId, CreateMarkdownPageRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateOwnerAndContentAsync(ownerId, request.Title, request.Markdown, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var page = new MarkdownPage
        {
            OwnerId = ownerId,
            Title = request.Title.Trim(),
            IsBuiltIn = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.MarkdownPages.Add(page);
        await db.SaveChangesAsync(cancellationToken);

        var version = new MarkdownPageVersion
        {
            PageId = page.Id,
            Version = 1,
            Title = page.Title,
            Markdown = request.Markdown,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.MarkdownPageVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        page.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDetail(page, version);
    }

    public async Task<MarkdownPageDetailDto?> UpdateAsync(
        string ownerId,
        Guid pageId,
        UpdateMarkdownPageRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateOwnerAndContentAsync(ownerId, request.Title, request.Markdown, cancellationToken);
        var page = await db.MarkdownPages.Include(item => item.CurrentVersion)
            .SingleOrDefaultAsync(item => item.Id == pageId && item.OwnerId == ownerId, cancellationToken);
        if (page is null) return null;
        if (page.IsBuiltIn) throw new MarkdownPageConflictException("The built-in page cannot be edited.");
        if (page.IsArchived) throw new MarkdownPageConflictException("Restore the page before editing it.");
        if (page.CurrentVersionId != request.ExpectedVersionId)
            throw new MarkdownPageConflictException("The page has changed since it was opened. Reload the current version before saving.");

        var next = new MarkdownPageVersion
        {
            PageId = page.Id,
            Version = (page.CurrentVersion?.Version ?? 0) + 1,
            Title = request.Title.Trim(),
            Markdown = request.Markdown,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.MarkdownPageVersions.Add(next);
        page.Title = next.Title;
        page.CurrentVersionId = next.Id;
        page.UpdatedAt = next.CreatedAt;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MarkdownPageConflictException("The page has changed since it was opened. Reload the current version before saving.");
        }
        return ToDetail(page, next);
    }

    public async Task<IReadOnlyList<MarkdownPageVersionDto>?> ListVersionsAsync(string ownerId, Guid pageId, CancellationToken cancellationToken = default)
    {
        if (!await db.MarkdownPages.AnyAsync(page => page.Id == pageId && page.OwnerId == ownerId, cancellationToken)) return null;
        return await db.MarkdownPageVersions.AsNoTracking()
            .Where(version => version.PageId == pageId)
            .OrderByDescending(version => version.Version)
            .Select(version => ToVersion(version))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MarkdownPreviewDto> PreviewAsync(string ownerId, string markdown, CancellationToken cancellationToken = default)
    {
        ValidateMarkdown(markdown);
        if (!await assets.OwnsAllReferencedAssetsAsync(ownerId, markdown, cancellationToken))
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["markdown"] = ["An image reference is not owned by this account."] });
        return new MarkdownPreviewDto(renderer.Render(markdown, MarkdownAssetService.PreviewUrl));
    }

    public async Task<bool> ArchiveAsync(string ownerId, Guid pageId, CancellationToken cancellationToken = default)
    {
        var page = await db.MarkdownPages.SingleOrDefaultAsync(item => item.Id == pageId && item.OwnerId == ownerId, cancellationToken);
        if (page is null) return false;
        if (page.IsBuiltIn) throw new MarkdownPageConflictException("The built-in page cannot be archived.");
        if (await IsReferencedByCaseAsync(page.Id, cancellationToken))
            throw new MarkdownPageConflictException("Remove this page from every learning set before archiving it.");
        page.IsArchived = true;
        page.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(string ownerId, Guid pageId, CancellationToken cancellationToken = default)
    {
        var page = await db.MarkdownPages.SingleOrDefaultAsync(item => item.Id == pageId && item.OwnerId == ownerId, cancellationToken);
        if (page is null) return false;
        if (page.IsBuiltIn) throw new MarkdownPageConflictException("The built-in page cannot be changed.");
        page.IsArchived = false;
        page.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ValidateOwnerAndContentAsync(string ownerId, string title, string markdown, CancellationToken cancellationToken)
    {
        if (!await IsActiveTeacherAsync(ownerId))
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["owner"] = ["The page owner is unavailable."] });
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["title"] = ["Title must contain between 1 and 200 characters."] });
        ValidateMarkdown(markdown);
        if (!await assets.OwnsAllReferencedAssetsAsync(ownerId, markdown, cancellationToken))
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["markdown"] = ["An image reference is not owned by this account."] });
    }

    private async Task<bool> IsActiveTeacherAsync(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return false;
        var owner = await users.FindByIdAsync(ownerId);
        return owner is { IsActive: true } && await users.IsInRoleAsync(owner, BootstrapPlatformAdmin.TeacherRole);
    }

    private async Task<bool> IsReferencedByCaseAsync(Guid pageId, CancellationToken cancellationToken)
    {
        var hasItemsTable = await db.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass('public.case_content_items') IS NOT NULL AS \"Value\"").SingleAsync(cancellationToken);
        if (!hasItemsTable) return false;

        return await db.Database.SqlQueryRaw<bool>(
            "SELECT EXISTS (SELECT 1 FROM case_content_items item JOIN markdown_page_versions version ON version.\"Id\" = item.\"MarkdownPageVersionId\" WHERE version.\"PageId\" = {0}) AS \"Value\"",
            pageId).SingleAsync(cancellationToken);
    }

    private static void ValidateMarkdown(string markdown)
    {
        if (markdown is null || System.Text.Encoding.UTF8.GetByteCount(markdown) > MarkdownRenderer.MaximumMarkdownBytes)
            throw new MarkdownPageValidationException(new Dictionary<string, string[]> { ["markdown"] = [$"Markdown must be no larger than {MarkdownRenderer.MaximumMarkdownBytes} bytes."] });
    }

    private static MarkdownPageDetailDto ToDetail(MarkdownPage page, MarkdownPageVersion version) =>
        new(ToPageDto(page, version), ToVersion(version));

    private static MarkdownPageDto ToPageDto(MarkdownPage page, MarkdownPageVersion version) =>
        new(page.Id, page.Title, version.Id, version.Version, page.IsBuiltIn, page.IsArchived);

    private static MarkdownPageVersionDto ToVersion(MarkdownPageVersion version) =>
        new(version.Id, version.PageId, version.Version, version.Title, version.Markdown, version.CreatedAt);
}
