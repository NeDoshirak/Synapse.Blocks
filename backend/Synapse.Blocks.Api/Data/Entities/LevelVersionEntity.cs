namespace Synapse.Blocks.Api.Data.Entities;

/// <summary>An immutable snapshot of a level definition used by published case sets.</summary>
public sealed class LevelVersionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LevelId { get; set; }
    public int Version { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
