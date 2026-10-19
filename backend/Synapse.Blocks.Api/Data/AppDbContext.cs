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
    public DbSet<CaseContentItem> CaseContentItems => Set<CaseContentItem>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptLevelResult> AttemptLevelResults => Set<AttemptLevelResult>();
    public DbSet<MarkdownPage> MarkdownPages => Set<MarkdownPage>();
    public DbSet<MarkdownPageVersion> MarkdownPageVersions => Set<MarkdownPageVersion>();
    public DbSet<MarkdownAsset> MarkdownAssets => Set<MarkdownAsset>();

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
        teacherCase.Property(item => item.ProgressionMode).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Synapse.Blocks.Api.Contracts.Cases.CaseProgressionMode.Sequential);
        teacherCase.HasOne(item => item.Owner).WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.Sections).WithOne(section => section.Case).HasForeignKey(section => section.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany(item => item.ShareLinks).WithOne(link => link.Case).HasForeignKey(link => link.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany<Participant>().WithOne(participant => participant.Case).HasForeignKey(participant => participant.CaseId).OnDelete(DeleteBehavior.Restrict);
        teacherCase.HasMany<Attempt>().WithOne(attempt => attempt.Case).HasForeignKey(attempt => attempt.CaseId).OnDelete(DeleteBehavior.Restrict);

        var caseSection = modelBuilder.Entity<CaseSection>();
        caseSection.ToTable("case_sections");
        caseSection.HasKey(item => item.Id);
        caseSection.HasIndex(item => item.CaseId);
        caseSection.Property(item => item.Title).HasMaxLength(120);
        caseSection.HasOne(item => item.Case).WithMany(item => item.Sections).HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        caseSection.HasMany(item => item.Items).WithOne(content => content.Section).HasForeignKey(content => content.SectionId).OnDelete(DeleteBehavior.Restrict);

        var caseContentItem = modelBuilder.Entity<CaseContentItem>();
        caseContentItem.ToTable("case_content_items", table => table.HasCheckConstraint(
            "CK_case_content_items_kind_reference",
            "(\"Kind\" = 'level' AND \"LevelVersionId\" IS NOT NULL AND \"MarkdownPageVersionId\" IS NULL) OR (\"Kind\" = 'markdown' AND \"LevelVersionId\" IS NULL AND \"MarkdownPageVersionId\" IS NOT NULL)"));
        caseContentItem.HasKey(item => item.Id);
        caseContentItem.HasIndex(item => item.CaseId);
        caseContentItem.HasIndex(item => new { item.SectionId, item.Order }).IsUnique();
        caseContentItem.HasIndex(item => new { item.CaseId, item.LevelVersionId }).IsUnique().HasFilter("\"Kind\" = 'level'");
        caseContentItem.Property(item => item.Kind).HasMaxLength(20).IsRequired();
        caseContentItem.Property(item => item.DisplayTitle).HasMaxLength(200);
        caseContentItem.HasOne(item => item.Case).WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        caseContentItem.HasOne(item => item.LevelVersion).WithMany().HasForeignKey(item => item.LevelVersionId).OnDelete(DeleteBehavior.Restrict);
        caseContentItem.HasOne(item => item.MarkdownPageVersion).WithMany().HasForeignKey(item => item.MarkdownPageVersionId).OnDelete(DeleteBehavior.Restrict);

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
        attempt.Property(item => item.ProgressionMode).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Synapse.Blocks.Api.Contracts.Cases.CaseProgressionMode.Sequential);
        attempt.Property(item => item.LevelVersionIdsJson).HasColumnType("jsonb").IsRequired();
        attempt.Property(item => item.LevelPresentationJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        attempt.HasOne(item => item.Case).WithMany().HasForeignKey(item => item.CaseId).OnDelete(DeleteBehavior.Restrict);
        attempt.HasMany(item => item.LevelResults).WithOne(result => result.Attempt).HasForeignKey(result => result.AttemptId).OnDelete(DeleteBehavior.Restrict);
        attempt.HasMany(item => item.Steps).WithOne(step => step.Attempt).HasForeignKey(step => step.AttemptId).OnDelete(DeleteBehavior.Restrict);

        var attemptResult = modelBuilder.Entity<AttemptLevelResult>();
        attemptResult.ToTable("attempt_level_results");
        attemptResult.HasKey(item => item.Id);
        attemptResult.HasIndex(item => new { item.AttemptId, item.LevelVersionId }).IsUnique();
        attemptResult.HasOne(item => item.LevelVersion).WithMany().HasForeignKey(item => item.LevelVersionId).OnDelete(DeleteBehavior.Restrict);

        var attemptStep = modelBuilder.Entity<AttemptStep>();
        attemptStep.ToTable("attempt_steps", table => table.HasCheckConstraint(
            "CK_attempt_steps_kind_reference",
            "(\"Kind\" = 'level' AND \"LevelVersionId\" IS NOT NULL AND \"MarkdownPageVersionId\" IS NULL AND \"Markdown\" IS NULL) OR (\"Kind\" = 'markdown' AND \"LevelVersionId\" IS NULL AND \"MarkdownPageVersionId\" IS NOT NULL AND \"Markdown\" IS NOT NULL)"));
        attemptStep.HasKey(item => item.Id);
        attemptStep.HasIndex(item => new { item.AttemptId, item.Order }).IsUnique();
        attemptStep.HasIndex(item => new { item.AttemptId, item.SourceItemId }).IsUnique();
        attemptStep.Property(item => item.Kind).HasMaxLength(20).IsRequired();
        attemptStep.Property(item => item.SectionTitle).HasMaxLength(120);
        attemptStep.Property(item => item.Title).HasMaxLength(200).IsRequired();
        attemptStep.Property(item => item.Markdown).HasMaxLength(102400);
        attemptStep.HasOne(item => item.LevelVersion).WithMany().HasForeignKey(item => item.LevelVersionId).OnDelete(DeleteBehavior.Restrict);
        attemptStep.HasOne(item => item.MarkdownPageVersion).WithMany().HasForeignKey(item => item.MarkdownPageVersionId).OnDelete(DeleteBehavior.Restrict);

        var markdownPage = modelBuilder.Entity<MarkdownPage>();
        markdownPage.ToTable("markdown_pages");
        markdownPage.HasKey(item => item.Id);
        markdownPage.HasIndex(item => new { item.OwnerId, item.IsArchived, item.Title });
        markdownPage.HasIndex(item => item.BuiltInKey).IsUnique();
        markdownPage.Property(item => item.Title).HasMaxLength(200).IsRequired();
        markdownPage.Property(item => item.BuiltInKey).HasMaxLength(100);
        markdownPage.Property(item => item.CurrentVersionId).IsConcurrencyToken();
        markdownPage.HasOne(item => item.Owner).WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        markdownPage.HasMany(item => item.Versions).WithOne(version => version.Page).HasForeignKey(version => version.PageId).OnDelete(DeleteBehavior.Restrict);
        markdownPage.HasOne(item => item.CurrentVersion).WithMany().HasForeignKey(item => item.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);

        var markdownPageVersion = modelBuilder.Entity<MarkdownPageVersion>();
        markdownPageVersion.ToTable("markdown_page_versions");
        markdownPageVersion.HasKey(item => item.Id);
        markdownPageVersion.HasIndex(item => new { item.PageId, item.Version }).IsUnique();
        markdownPageVersion.Property(item => item.Title).HasMaxLength(200).IsRequired();
        markdownPageVersion.Property(item => item.Markdown).HasMaxLength(102400).IsRequired();

        var markdownAsset = modelBuilder.Entity<MarkdownAsset>();
        markdownAsset.ToTable("markdown_assets", table => table.HasCheckConstraint(
            "CK_markdown_assets_length", "\"Length\" > 0 AND \"Length\" <= 10485760"));
        markdownAsset.HasKey(item => item.Id);
        markdownAsset.HasIndex(item => item.OwnerId);
        markdownAsset.Property(item => item.ContentType).HasMaxLength(20).IsRequired();
        markdownAsset.Property(item => item.Content).HasColumnType("bytea").IsRequired();
        markdownAsset.HasOne(item => item.Owner).WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
