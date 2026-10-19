namespace Synapse.Blocks.Api.Contracts.MarkdownPages;

public sealed record MarkdownPageDto(
    Guid Id,
    string Title,
    Guid CurrentVersionId,
    int Version,
    bool IsBuiltIn,
    bool IsArchived);

public sealed record MarkdownPageVersionDto(
    Guid Id,
    Guid PageId,
    int Version,
    string Title,
    string Markdown,
    DateTimeOffset CreatedAt);

public sealed record MarkdownPageDetailDto(MarkdownPageDto Page, MarkdownPageVersionDto CurrentVersion);

public sealed record CreateMarkdownPageRequest(string Title, string Markdown);

public sealed record UpdateMarkdownPageRequest(Guid ExpectedVersionId, string Title, string Markdown);

public sealed record MarkdownPreviewRequest(string Markdown);

public sealed record MarkdownPreviewDto(string Html);

public sealed record MarkdownAssetDto(Guid Id, string ContentType, long Length, string Url);

public sealed record MarkdownAssetContent(string ContentType, byte[] Content, DateTimeOffset CreatedAt);

public sealed class MarkdownPageValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Markdown page request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class MarkdownPageConflictException(string message) : Exception(message);
