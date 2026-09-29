using Synapse.Blocks.Api.Auth;

namespace Synapse.Blocks.Api.Data.Entities;

public sealed class TeacherLevel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public ApplicationUser Owner { get; set; } = null!;
    public string Title { get; set; } = "";
    public Guid? CurrentVersionId { get; set; }
    public LevelVersionEntity? CurrentVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<LevelVersionEntity> Versions { get; set; } = [];
}
