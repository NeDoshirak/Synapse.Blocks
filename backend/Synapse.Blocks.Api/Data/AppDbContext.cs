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
    }
}
