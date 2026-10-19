namespace Synapse.Blocks.Api.Data.Entities;

public sealed class AttemptStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public Attempt Attempt { get; set; } = null!;
    public Guid SourceItemId { get; set; }
    public int Order { get; set; }
    public string Kind { get; set; } = "";
    public int? SectionOrder { get; set; }
    public string? SectionTitle { get; set; }
    public string Title { get; set; } = "";
    public Guid? LevelVersionId { get; set; }
    public LevelVersionEntity? LevelVersion { get; set; }
    public Guid? MarkdownPageVersionId { get; set; }
    public MarkdownPageVersion? MarkdownPageVersion { get; set; }
    public string? Markdown { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
