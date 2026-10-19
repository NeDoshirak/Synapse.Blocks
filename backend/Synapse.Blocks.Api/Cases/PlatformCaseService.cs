using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Contracts.Levels;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Api.Levels;

namespace Synapse.Blocks.Api.Cases;

public sealed class PlatformCaseService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    CaseService cases,
    ShareLinkService shareLinks,
    LevelService levels,
    PlatformAuditEventService audit)
{
    public async Task<IReadOnlyList<PlatformCaseDto>> ListAsync(
        string? search,
        string? ownerId,
        string? state,
        CancellationToken cancellationToken = default)
    {
        var query = CaseQuery();
        if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(item => item.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(state))
        {
            query = state.Trim().ToLowerInvariant() switch
            {
                "draft" => query.Where(item => !item.Archived && !item.IsPublished),
                "published" => query.Where(item => !item.Archived && item.IsPublished),
                "archived" => query.Where(item => item.Archived),
                _ => query
            };
        }
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var normalized = term.ToUpperInvariant();
            query = query.Where(item =>
                item.Title.ToUpper().Contains(normalized)
                || (item.Owner.Email ?? "").ToUpper().Contains(normalized)
                || item.Owner.DisplayName.ToUpper().Contains(normalized)
                || (item.Owner.FirstName ?? "").ToUpper().Contains(normalized)
                || (item.Owner.LastName ?? "").ToUpper().Contains(normalized));
        }

        var entities = await query.OrderByDescending(item => item.UpdatedAt).ToListAsync(cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task<PlatformCaseDto?> GetAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var entity = await CaseQuery().FirstOrDefaultAsync(item => item.Id == caseId, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<TeacherLevelDto>?> ListOwnerLevelsAsync(string teacherId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teacherId)) return null;
        var teacher = await users.FindByIdAsync(teacherId);
        if (teacher is null || !await users.IsInRoleAsync(teacher, BootstrapPlatformAdmin.TeacherRole)) return null;
        return await levels.ListAsync(teacherId, cancellationToken);
    }

    public async Task<PlatformCaseDto?> CreateAsync(
        string ownerId,
        CreateCaseRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return null;
        var owner = await FindTeacherAsync(ownerId);
        if (owner is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var created = await cases.CreateAsync(ownerId, request, cancellationToken);
        await audit.RecordAsync(actorId, "case.created.by-platform-admin", "case", created.Id.ToString(), cancellationToken);
        var result = await GetAsync(created.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PlatformCaseDto?> UpdateAsync(
        Guid caseId,
        UpdateCaseRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ownerId = await db.TeacherCases.Where(item => item.Id == caseId).Select(item => item.OwnerId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null) return null;
        var updated = await cases.UpdateAsync(ownerId, caseId, request, cancellationToken);
        if (updated is null) return null;
        await audit.RecordAsync(actorId, "case.updated.by-platform-admin", "case", caseId.ToString(), cancellationToken);
        var result = await GetAsync(caseId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PlatformCaseDto?> PublishAsync(Guid caseId, string actorId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ownerId = await db.TeacherCases.Where(item => item.Id == caseId).Select(item => item.OwnerId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null) return null;
        var published = await cases.PublishAsync(ownerId, caseId, cancellationToken);
        if (published is null) return null;
        await audit.RecordAsync(actorId, "case.published.by-platform-admin", "case", caseId.ToString(), cancellationToken);
        var result = await GetAsync(caseId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> ArchiveAsync(Guid caseId, string actorId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ownerId = await db.TeacherCases.Where(item => item.Id == caseId).Select(item => item.OwnerId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null || !await cases.ArchiveAsync(ownerId, caseId, cancellationToken)) return false;
        await audit.RecordAsync(actorId, "case.archived.by-platform-admin", "case", caseId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(Guid caseId, string actorId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ownerId = await db.TeacherCases.Where(item => item.Id == caseId).Select(item => item.OwnerId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null || !await cases.RestoreAsync(ownerId, caseId, cancellationToken)) return false;
        await audit.RecordAsync(actorId, "case.restored.by-platform-admin", "case", caseId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ShareLinkCreatedDto?> CreateOrRotateShareLinkAsync(
        Guid caseId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var ownerId = await db.TeacherCases.Where(item => item.Id == caseId).Select(item => item.OwnerId).SingleOrDefaultAsync(cancellationToken);
        return ownerId is null ? null : await shareLinks.CreateOrRotateAsync(ownerId, caseId, cancellationToken, actorId);
    }

    public async Task<bool> RevokeShareLinkAsync(Guid caseId, string actorId, CancellationToken cancellationToken = default)
    {
        var ownerId = await db.TeacherCases.Where(item => item.Id == caseId).Select(item => item.OwnerId).SingleOrDefaultAsync(cancellationToken);
        return ownerId is not null && await shareLinks.RevokeAsync(ownerId, caseId, cancellationToken, actorId);
    }

    private IQueryable<TeacherCase> CaseQuery() => db.TeacherCases.AsNoTracking()
        .Include(item => item.Owner)
        .Include(item => item.Sections).ThenInclude(section => section.Levels).ThenInclude(level => level.LevelVersion)
        .Include(item => item.ShareLinks);

    private async Task<ApplicationUser?> FindTeacherAsync(string ownerId)
    {
        var owner = await users.FindByIdAsync(ownerId);
        return owner is not null && await users.IsInRoleAsync(owner, BootstrapPlatformAdmin.TeacherRole) ? owner : null;
    }

    private static PlatformCaseDto ToDto(TeacherCase item)
    {
        var sections = item.Sections.OrderBy(section => section.Order).Select(section => new CaseSectionDto(
            section.Id,
            section.Title,
            section.Order,
            section.Levels.OrderBy(level => level.Order).Select(level => new CaseLevelDto(
                level.LevelId,
                level.LevelVersionId,
                level.Order,
                level.LevelVersion.Title,
                level.DisplayTitle)).ToArray())).ToArray();
        var ownerName = $"{item.Owner.FirstName} {item.Owner.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(ownerName)) ownerName = string.IsNullOrWhiteSpace(item.Owner.DisplayName) ? item.Owner.Email ?? "Преподаватель" : item.Owner.DisplayName;
        return new PlatformCaseDto(
            item.Id,
            item.OwnerId,
            ownerName,
            item.Owner.Email ?? item.Owner.UserName ?? "",
            item.CaseType,
            item.Title,
            item.Description,
            item.IsPublished,
            item.Archived,
            sections,
            sections.Sum(section => section.Levels.Count),
            item.ShareLinks.Any(link => link.RevokedAt is null),
            item.CreatedAt,
            item.UpdatedAt);
    }
}
