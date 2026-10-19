using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;
using Synapse.Blocks.Api.Contracts.MarkdownPages;

namespace Synapse.Blocks.Api.MarkdownPages;

public sealed class MarkdownRenderer
{
    public const int MaximumMarkdownBytes = 100 * 1024;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex AssetImageSource = new(
        "src=(?<quote>['\\\"])asset:(?<id>[0-9a-fA-F-]{36})\\k<quote>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MermaidCodeBlock = new(
        "<pre><code class=\"language-mermaid\">(?<diagram>[\\s\\S]*?)</code></pre>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "abbr", "b", "blockquote", "br", "caption", "code", "dd", "del", "details", "div", "dl", "dt",
        "em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "img", "input", "kbd", "li", "ol", "p",
        "pre", "s", "samp", "section", "small", "span", "strong", "sub", "summary", "sup", "table", "tbody",
        "td", "tfoot", "th", "thead", "tr", "u", "ul"
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "alt", "checked", "class", "colspan", "disabled", "href", "id", "rowspan", "scope", "src", "start", "title", "type"
    };

    private static readonly HashSet<string> AllowedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "language-mermaid", "mermaid", "math", "math-inline", "math-display", "task-list", "task-list-item", "contains-task-list"
    };

    public string Render(string markdown, Func<Guid, string> assetUrl)
    {
        ArgumentNullException.ThrowIfNull(assetUrl);
        markdown ??= "";
        if (System.Text.Encoding.UTF8.GetByteCount(markdown) > MaximumMarkdownBytes)
            throw new MarkdownPageValidationException(new Dictionary<string, string[]>
            {
                ["markdown"] = [$"Markdown must be no larger than {MaximumMarkdownBytes} bytes."]
            });

        var rendered = Markdown.ToHtml(markdown, Pipeline);
        rendered = MermaidCodeBlock.Replace(rendered, "<div class=\"mermaid\">${diagram}</div>");
        rendered = AssetImageSource.Replace(rendered, match =>
        {
            if (!Guid.TryParse(match.Groups["id"].Value, out var assetId)) return match.Value;
            var url = HtmlEncoder.Default.Encode(assetUrl(assetId));
            var quote = match.Groups["quote"].Value;
            return $"src={quote}{url}{quote}";
        });

        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(AllowedTags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(AllowedAttributes, StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(["http", "https"], StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(["href", "src"], StringComparer.OrdinalIgnoreCase),
            AllowedCssClasses = new HashSet<string>(AllowedClasses, StringComparer.OrdinalIgnoreCase),
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        });
        sanitizer.AllowedAtRules.Clear();
        return sanitizer.Sanitize(rendered);
    }
}
