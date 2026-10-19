namespace Synapse.Blocks.Models;

public sealed record CaseLevelMenuItem(
    Guid LevelId,
    Guid LevelVersionId,
    int Order,
    int? SectionOrder,
    string? SectionTitle,
    string DisplayTitle,
    bool IsCompleted,
    bool IsDisabled);
