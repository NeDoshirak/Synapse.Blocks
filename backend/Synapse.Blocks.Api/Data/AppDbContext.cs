using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<LevelVersionEntity> LevelVersions => Set<LevelVersionEntity>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<TeacherLevel> TeacherLevels => Set<TeacherLevel>();
    public DbSet<TeacherCase> TeacherCases => Set<TeacherCase>();
    public DbSet<CaseLevel> CaseLevels => Set<CaseLevel>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var levelVersion = modelBuilder.Entity<LevelVersionEntity>();
        levelVersion.ToTable("level_versions");
        levelVersion.HasKey(version => version.Id);
        levelVersion.HasIndex(version => new { version.LevelId, version.Version }).IsUnique();
        levelVersion.Property(version => version.DefinitionJson).HasColumnType("jsonb").IsRequired();

        var teacherLevel = modelBuilder.Entity<TeacherLevel>();
        teacherLevel.ToTable("teacher_levels");
        teacherLevel.HasKey(level => level.Id);
        teacherLevel.HasIndex(level => level.OwnerId);
        teacherLevel.Property(level => level.Title).HasMaxLength(200).IsRequired();
        teacherLevel.HasOne(level => level.Owner).WithMany().HasForeignKey(level => level.OwnerId).OnDelete(DeleteBehavior.Restrict);
        teacherLevel.HasMany(level => level.Versions).WithOne(version => version.Level).HasForeignKey(version => version.LevelId).OnDelete(DeleteBehavior.Restrict);
        teacherLevel.HasOne(level => level.CurrentVersion).WithMany().HasForeignKey(level => level.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);
        teacherLevel.Property(level => level.CurrentVersionId).IsConcurrencyToken();

        var invitation = modelBuilder.Entity<Invitation>();
        invitation.ToTable("invitations");
        invitation.HasKey(item => item.Id);
        invitation.HasIndex(item => item.TokenHash).IsUnique();
        invitation.Property(item => item.Email).HasMaxLength(256).IsRequired();
        invitation.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
        invitation.Property(item => item.InvitedByUserId).IsRequired();

        var teacherCase = modelBuilder.Entity<TeacherCase>();
        teacherCase.ToTable("teacher_cases");
        teacherCase.HasKey(item => item.Id);
        teacherCase.HasIndex(item => item.OwnerId);
        teacherCase.Property(item => item.CaseType).HasMaxLength(40).IsRequired();
        teacherCase.Property(item => item.Title).HasMaxLength(200).IsRequired();
        teacherCase.Property(item => item.Description).HasMaxLength(2000).IsRequired();
        teacherCase.HasOne(item => item.Owner).WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.Levels).WithOne(level => level.Case).HasForeignKey(level => level.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.ShareLinks).WithOne(link => link.Case).HasForeignKey(link => link.CaseId).OnDelete(DeleteBehavior.Restrict);

        var caseLevel = modelBuilder.Entity<CaseLevel>();
        caseLevel.ToTable("case_levels");
        caseLevel.HasKey(item => item.Id);
        caseLevel.HasIndex(item => new { item.CaseId, item.Order }).IsUnique();
        caseLevel.HasOne(item => item.LevelVersion).WithMany().HasForeignKey(item => item.LevelVersionId).OnDelete(DeleteBehavior.Restrict);

        var shareLink = modelBuilder.Entity<ShareLink>();
        shareLink.ToTable("share_links");
        shareLink.HasKey(item => item.Id);
        shareLink.HasIndex(item => item.TokenHash).IsUnique();
        shareLink.HasIndex(item => item.CaseId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        shareLink.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
    }
}
