using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.MarkdownPages;
using Synapse.Blocks.Api.MarkdownPages;
using Synapse.Blocks.Api.Operations;

namespace Synapse.Blocks.Api.Endpoints;

public static class TeacherMarkdownPageEndpoints
{
    public static IEndpointRouteBuilder MapTeacherMarkdownPageEndpoints(this IEndpointRouteBuilder app)
    {
        var pages = app.MapGroup("/api/teacher/markdown-pages")
            .RequireAuthorization("ActiveMfa")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        pages.MapGet("/", async (ICurrentUser current, MarkdownPageService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(current.UserId, ct)));

        pages.MapGet("/{pageId:guid}", async (Guid pageId, ICurrentUser current, MarkdownPageService service, CancellationToken ct) =>
        {
            var page = await service.GetAsync(current.UserId, pageId, ct);
            return page is null ? Results.NotFound() : Results.Ok(page);
        });

        pages.MapGet("/{pageId:guid}/versions", async (Guid pageId, ICurrentUser current, MarkdownPageService service, CancellationToken ct) =>
        {
            var versions = await service.ListVersionsAsync(current.UserId, pageId, ct);
            return versions is null ? Results.NotFound() : Results.Ok(versions);
        });

        pages.MapPost("/", async (
            CreateMarkdownPageRequest request,
            ICurrentUser current,
            MarkdownPageService service,
            CancellationToken ct) =>
        {
            try
            {
                var page = await service.CreateAsync(current.UserId, request, ct);
                return Results.Created($"/api/teacher/markdown-pages/{page.Page.Id}", page);
            }
            catch (MarkdownPageValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });

        pages.MapPut("/{pageId:guid}", async (
            Guid pageId,
            UpdateMarkdownPageRequest request,
            ICurrentUser current,
            MarkdownPageService service,
            CancellationToken ct) =>
        {
            try
            {
                var page = await service.UpdateAsync(current.UserId, pageId, request, ct);
                return page is null ? Results.NotFound() : Results.Ok(page);
            }
            catch (MarkdownPageValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
            catch (MarkdownPageConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        pages.MapPost("/preview", async (
            MarkdownPreviewRequest request,
            ICurrentUser current,
            MarkdownPageService service,
            CancellationToken ct) =>
        {
            try { return Results.Ok(await service.PreviewAsync(current.UserId, request.Markdown, ct)); }
            catch (MarkdownPageValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });

        pages.MapPost("/{pageId:guid}/archive", async (Guid pageId, ICurrentUser current, MarkdownPageService service, CancellationToken ct) =>
        {
            try { return await service.ArchiveAsync(current.UserId, pageId, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (MarkdownPageConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        pages.MapPost("/{pageId:guid}/restore", async (Guid pageId, ICurrentUser current, MarkdownPageService service, CancellationToken ct) =>
        {
            try { return await service.RestoreAsync(current.UserId, pageId, ct) ? Results.NoContent() : Results.NotFound(); }
            catch (MarkdownPageConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        var assets = app.MapGroup("/api/teacher/markdown-assets")
            .RequireAuthorization("ActiveMfa")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        assets.MapPost("/", async (HttpRequest request, ICurrentUser current, MarkdownAssetService service, CancellationToken ct) =>
        {
            try
            {
                var form = await request.ReadFormAsync(ct);
                if (form.Files.Count != 1) return Results.BadRequest(new { error = "Upload exactly one image." });
                var file = form.Files[0];
                if (file.Length is <= 0 or > MarkdownAssetService.MaximumAssetBytes)
                    return Results.UnprocessableEntity(new { errors = new { file = new[] { "Image must be between 1 byte and 10 MiB." } } });
                await using var stream = file.OpenReadStream();
                var uploaded = await service.UploadAsync(current.UserId, stream, file.ContentType, ct);
                return Results.Created(uploaded.Url, uploaded);
            }
            catch (MarkdownPageValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
            catch (InvalidDataException) { return Results.BadRequest(new { error = "Malformed multipart image upload." }); }
        });

        assets.MapGet("/{assetId:guid}/content", async (
            Guid assetId,
            ICurrentUser current,
            MarkdownAssetService service,
            HttpContext context,
            CancellationToken ct) =>
        {
            var asset = await service.GetForPreviewAsync(current.UserId, assetId, ct);
            if (asset is null) return Results.NotFound();
            context.Response.Headers["Cache-Control"] = "private, max-age=86400, immutable";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(asset.Content, asset.ContentType);
        });

        return app;
    }
}
