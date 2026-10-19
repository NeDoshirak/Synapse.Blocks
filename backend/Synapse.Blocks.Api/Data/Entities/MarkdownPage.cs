using Synapse.Blocks.Api.Auth;

namespace Synapse.Blocks.Api.Data.Entities;

public sealed class MarkdownPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public ApplicationUser Owner { get; set; } = null!;
    public string Title { get; set; } = "";
    public Guid? CurrentVersionId { get; set; }
    public MarkdownPageVersion? CurrentVersion { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? BuiltInKey { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<MarkdownPageVersion> Versions { get; set; } = [];
}
