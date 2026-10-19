namespace Synapse.Blocks.Models;

public sealed record CaseMenuItem(
    Guid ItemId,
    Guid? LevelVersionId,
    string Kind,
    int Order,
    int? SectionOrder,
    string? SectionTitle,
    string DisplayTitle,
    bool IsCompleted,
    bool IsDisabled);
