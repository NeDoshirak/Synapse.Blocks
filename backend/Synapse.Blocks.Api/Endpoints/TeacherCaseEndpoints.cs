using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Cases;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Operations;

namespace Synapse.Blocks.Api.Endpoints;

public static class TeacherCaseEndpoints
{
    public static IEndpointRouteBuilder MapTeacherCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teacher/cases").RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapGet("/", async (ICurrentUser current, CaseService cases, CancellationToken ct) => Results.Ok(await cases.ListAsync(current.UserId, ct)));
        group.MapPost("/", async (CreateCaseRequest request, ICurrentUser current, CaseService cases, CancellationToken ct) =>
        {
            try
            {
                var created = await cases.CreateAsync(current.UserId, request, ct);
                return Results.Created($"/api/teacher/cases/{created.Id}", created);
            }
            catch (CaseValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });
        group.MapGet("/{caseId:guid}", async (Guid caseId, ICurrentUser current, CaseService cases, CancellationToken ct) =>
        {
            var item = await cases.GetAsync(current.UserId, caseId, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });
        group.MapPut("/{caseId:guid}", async (Guid caseId, UpdateCaseRequest request, ICurrentUser current, CaseService cases, CancellationToken ct) =>
        {
            try
            {
                var updated = await cases.UpdateAsync(current.UserId, caseId, request, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (CaseValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });
        group.MapPost("/{caseId:guid}/publish", async (Guid caseId, ICurrentUser current, CaseService cases, CancellationToken ct) =>
        {
            try
            {
                var published = await cases.PublishAsync(current.UserId, caseId, ct);
                return published is null ? Results.NotFound() : Results.Ok(published);
            }
            catch (CaseValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });
        group.MapDelete("/{caseId:guid}", async (Guid caseId, ICurrentUser current, CaseService cases, CancellationToken ct) =>
            await cases.ArchiveAsync(current.UserId, caseId, ct) ? Results.NoContent() : Results.NotFound());
        group.MapPost("/{caseId:guid}/restore", async (Guid caseId, ICurrentUser current, CaseArchiveService archives, CancellationToken ct) =>
            await archives.RestoreAsync(current.UserId, caseId, ct) ? Results.NoContent() : Results.NotFound());
        group.MapPost("/{caseId:guid}/share-link", async (Guid caseId, ICurrentUser current, ShareLinkService links, CancellationToken ct) =>
        {
            var created = await links.CreateOrRotateAsync(current.UserId, caseId, ct);
            return created is null ? Results.NotFound() : Results.Ok(created);
        });
        group.MapDelete("/{caseId:guid}/share-link", async (Guid caseId, ICurrentUser current, ShareLinkService links, CancellationToken ct) =>
            await links.RevokeAsync(current.UserId, caseId, ct) ? Results.NoContent() : Results.NotFound());
        return app;
    }
}
