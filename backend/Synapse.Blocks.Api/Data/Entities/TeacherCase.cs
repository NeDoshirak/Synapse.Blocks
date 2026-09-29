using Synapse.Blocks.Api.Auth;

namespace Synapse.Blocks.Api.Data.Entities;

public sealed class TeacherCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public ApplicationUser Owner { get; set; } = null!;
    public string CaseType { get; set; } = "orderedLevels";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsPublished { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CaseLevel> Levels { get; set; } = [];
    public List<ShareLink> ShareLinks { get; set; } = [];
}
