namespace Synapse.Blocks.Api.Data.Entities;

public sealed class CaseSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public TeacherCase Case { get; set; } = null!;
    public string? Title { get; set; }
    public int Order { get; set; }
    public List<CaseContentItem> Items { get; set; } = [];
}
