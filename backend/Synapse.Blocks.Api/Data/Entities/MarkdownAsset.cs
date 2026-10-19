using Synapse.Blocks.Api.Auth;

namespace Synapse.Blocks.Api.Data.Entities;

public sealed class MarkdownAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public ApplicationUser Owner { get; set; } = null!;
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public byte[] Content { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
