using Synapse.Blocks.Api.Contracts.Cases;

namespace Synapse.Blocks.Api.Data.Entities;

public sealed class CaseContentItem
{
    public const string LevelKind = "level";
    public const string MarkdownKind = "markdown";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public TeacherCase Case { get; set; } = null!;
    public Guid SectionId { get; set; }
    public CaseSection Section { get; set; } = null!;
    public string Kind { get; set; } = LevelKind;
    public Guid? LevelVersionId { get; set; }
    public LevelVersionEntity? LevelVersion { get; set; }
    public Guid? MarkdownPageVersionId { get; set; }
    public MarkdownPageVersion? MarkdownPageVersion { get; set; }
    public int Order { get; set; }
    public string? DisplayTitle { get; set; }
}
