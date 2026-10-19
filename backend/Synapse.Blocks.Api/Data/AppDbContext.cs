using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Data.Entities;

namespace Synapse.Blocks.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<LevelVersionEntity> LevelVersions => Set<LevelVersionEntity>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<PlatformAuditEvent> PlatformAuditEvents => Set<PlatformAuditEvent>();
    public DbSet<TeacherLevel> TeacherLevels => Set<TeacherLevel>();
    public DbSet<TeacherCase> TeacherCases => Set<TeacherCase>();
    public DbSet<CaseSection> CaseSections => Set<CaseSection>();
    public DbSet<CaseLevel> CaseLevels => Set<CaseLevel>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptLevelResult> AttemptLevelResults => Set<AttemptLevelResult>();

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

        var applicationUser = modelBuilder.Entity<ApplicationUser>();
        applicationUser.Property(user => user.FirstName).HasMaxLength(100);
        applicationUser.Property(user => user.LastName).HasMaxLength(100);
        applicationUser.Property(user => user.TeacherDescription).HasMaxLength(1000);
        applicationUser.Property(user => user.IsActive).HasDefaultValue(true).IsRequired();

        invitation.Property(item => item.RevokedAt);

        var platformAudit = modelBuilder.Entity<PlatformAuditEvent>();
        platformAudit.ToTable("platform_audit_events");
        platformAudit.HasKey(item => item.Id);
        platformAudit.HasIndex(item => item.CreatedAt);
        platformAudit.HasIndex(item => new { item.TargetType, item.TargetId, item.CreatedAt });
        platformAudit.Property(item => item.Action).HasMaxLength(64).IsRequired();
        platformAudit.Property(item => item.TargetType).HasMaxLength(64).IsRequired();
        platformAudit.Property(item => item.TargetId).HasMaxLength(256).IsRequired();
        platformAudit.HasOne(item => item.Actor).WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        var teacherCase = modelBuilder.Entity<TeacherCase>();
        teacherCase.ToTable("teacher_cases");
        teacherCase.HasKey(item => item.Id);
        teacherCase.HasIndex(item => item.OwnerId);
        teacherCase.Property(item => item.CaseType).HasMaxLength(40).IsRequired();
        teacherCase.Property(item => item.Title).HasMaxLength(200).IsRequired();
        teacherCase.Property(item => item.Description).HasMaxLength(2000).IsRequired();
        teacherCase.HasOne(item => item.Owner).WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.Levels).WithOne(level => level.Case).HasForeignKey(level => level.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.Sections).WithOne(section => section.Case).HasForeignKey(section => section.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.ShareLinks).WithOne(link => link.Case).HasForeignKey(link => link.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany<Participant>().WithOne(participant => participant.Case).HasForeignKey(participant => participant.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany<Attempt>().WithOne(attempt => attempt.Case).HasForeignKey(attempt => attempt.CaseId).OnDelete(DeleteBehavior.Restrict);

        var caseLevel = modelBuilder.Entity<CaseLevel>();
        caseLevel.ToTable("case_levels");
        caseLevel.HasKey(item => item.Id);
        caseLevel.HasIndex(item => item.CaseId);
        caseLevel.HasIndex(item => new { item.SectionId, item.Order }).IsUnique();
        caseLevel.Property(item => item.DisplayTitle).HasMaxLength(200);
        caseLevel.HasOne(item => item.LevelVersion).WithMany().HasForeignKey(item => item.LevelVersionId).OnDelete(DeleteBehavior.Restrict);
        caseLevel.HasOne(item => item.Section).WithMany(section => section.Levels).HasForeignKey(item => item.SectionId).OnDelete(DeleteBehavior.Restrict);

        var caseSection = modelBuilder.Entity<CaseSection>();
        caseSection.ToTable("case_sections");
        caseSection.HasKey(item => item.Id);
        caseSection.HasIndex(item => item.CaseId);
        caseSection.Property(item => item.Title).HasMaxLength(120);
        caseSection.HasOne(item => item.Case).WithMany(item => item.Sections).HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);

        var shareLink = modelBuilder.Entity<ShareLink>();
        shareLink.ToTable("share_links");
        shareLink.HasKey(item => item.Id);
        shareLink.HasIndex(item => item.TokenHash).IsUnique();
        shareLink.HasIndex(item => item.CaseId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        shareLink.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();

        var participant = modelBuilder.Entity<Participant>();
        participant.ToTable("participants");
        participant.HasKey(item => item.Id);
        participant.HasIndex(item => new { item.CaseId, item.ContinuationHash }).IsUnique();
        participant.HasIndex(item => new { item.CaseId, item.NormalizedName });
        participant.Property(item => item.DisplayName).HasMaxLength(80).IsRequired();
        participant.Property(item => item.NormalizedName).HasMaxLength(80).IsRequired();
        participant.Property(item => item.ContinuationHash).HasMaxLength(64).IsRequired();
        participant.HasOne(item => item.Case).WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        participant.HasMany(item => item.Attempts).WithOne(attempt => attempt.Participant).HasForeignKey(attempt => attempt.ParticipantId).OnDelete(DeleteBehavior.Restrict);

        var attempt = modelBuilder.Entity<Attempt>();
        attempt.ToTable("attempts");
        attempt.HasKey(item => item.Id);
        attempt.HasIndex(item => new { item.CaseId, item.ParticipantId, item.StartedAt });
        attempt.Property(item => item.Status).HasMaxLength(20).IsRequired();
        attempt.Property(item => item.LevelVersionIdsJson).HasColumnType("jsonb").IsRequired();
        attempt.Property(item => item.LevelPresentationJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        attempt.HasOne(item => item.Case).WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        attempt.HasMany(item => item.LevelResults).WithOne(result => result.Attempt).HasForeignKey(result => result.AttemptId).OnDelete(DeleteBehavior.Restrict);

        var attemptResult = modelBuilder.Entity<AttemptLevelResult>();
        attemptResult.ToTable("attempt_level_results");
        attemptResult.HasKey(item => item.Id);
        attemptResult.HasIndex(item => new { item.AttemptId, item.LevelVersionId }).IsUnique();
        attemptResult.HasOne(item => item.LevelVersion).WithMany().HasForeignKey(item => item.LevelVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
