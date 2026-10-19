namespace Synapse.Blocks.Api.Data.Entities;

public sealed class MarkdownPageVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageId { get; set; }
    public MarkdownPage Page { get; set; } = null!;
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public string Markdown { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
