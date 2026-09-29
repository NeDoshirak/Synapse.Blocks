using Synapse.Blocks.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Synapse.Blocks.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LevelVersionEntity> LevelVersions => Set<LevelVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var levelVersion = modelBuilder.Entity<LevelVersionEntity>();
        levelVersion.ToTable("level_versions");
        levelVersion.HasKey(version => version.Id);
        levelVersion.HasIndex(version => new { version.LevelId, version.Version }).IsUnique();
        levelVersion.Property(version => version.DefinitionJson).HasColumnType("jsonb").IsRequired();
    }
}
