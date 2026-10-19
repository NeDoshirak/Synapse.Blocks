using Synapse.Blocks.Api.Cases;

namespace Synapse.Blocks.Api.Endpoints;

public static class StudentCaseEndpoints
{
    public static IEndpointRouteBuilder MapStudentCaseEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/student/cases/{token}", async (string token, ShareLinkService links, CancellationToken ct) =>
        {
            var item = await links.ResolveAsync(token, ct);
            return item is null ? Results.NotFound(new { error = "Case unavailable." }) : Results.Ok(item);
        }).AllowAnonymous();

        app.MapGet("/api/student/cases/{token}/markdown-assets/{assetId:guid}/content", async (
            string token,
            Guid assetId,
            ShareLinkService links,
            HttpContext context,
            CancellationToken ct) =>
        {
            var asset = await links.GetCaseMarkdownAssetAsync(token, assetId, ct);
            if (asset is null) return Results.NotFound();
            context.Response.Headers["Cache-Control"] = "private, max-age=86400, immutable";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(asset.Content, asset.ContentType);
        }).AllowAnonymous();
        return app;
    }
}
