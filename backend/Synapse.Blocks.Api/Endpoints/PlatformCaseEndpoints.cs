using System.Security.Claims;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Cases;
using Synapse.Blocks.Api.Contracts.Cases;

namespace Synapse.Blocks.Api.Endpoints;

public static class PlatformCaseEndpoints
{
    public static IEndpointRouteBuilder MapPlatformCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/platform")
            .RequireAuthorization(policy => policy.RequireRole(BootstrapPlatformAdmin.PlatformAdminRole))
            .RequireAuthorization("ActiveMfa")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapGet("/cases", async (string? search, string? ownerId, string? state, PlatformCaseService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, ownerId, state, ct)));

        group.MapGet("/teachers/{teacherId}/levels", async (string teacherId, PlatformCaseService service, CancellationToken ct) =>
        {
            var levels = await service.ListOwnerLevelsAsync(teacherId, ct);
            return levels is null ? Results.NotFound() : Results.Ok(levels);
        });

        group.MapGet("/cases/{caseId:guid}", async (Guid caseId, PlatformCaseService service, CancellationToken ct) =>
        {
            var item = await service.GetAsync(caseId, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("/cases", async (
            PlatformCreateCaseRequest request,
            ClaimsPrincipal principal,
            PlatformCaseService service,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var created = await service.CreateAsync(request.OwnerId,
                    new CreateCaseRequest(request.CaseType, request.Title, request.Description, request.Sections, request.ProgressionMode), actorId, ct);
                return created is null ? Results.NotFound() : Results.Created($"/api/platform/cases/{created.Id}", created);
            }
            catch (CaseValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });

        group.MapPut("/cases/{caseId:guid}", async (
            Guid caseId,
            UpdateCaseRequest request,
            ClaimsPrincipal principal,
            PlatformCaseService service,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var updated = await service.UpdateAsync(caseId, request, actorId, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (CaseValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });

        group.MapPost("/cases/{caseId:guid}/publish", async (Guid caseId, ClaimsPrincipal principal, PlatformCaseService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var published = await service.PublishAsync(caseId, actorId, ct);
                return published is null ? Results.NotFound() : Results.Ok(published);
            }
            catch (CaseValidationException ex) { return Results.UnprocessableEntity(new { errors = ex.Errors }); }
        });

        group.MapDelete("/cases/{caseId:guid}", async (Guid caseId, ClaimsPrincipal principal, PlatformCaseService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await service.ArchiveAsync(caseId, actorId, ct) ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/cases/{caseId:guid}/restore", async (Guid caseId, ClaimsPrincipal principal, PlatformCaseService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await service.RestoreAsync(caseId, actorId, ct) ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/cases/{caseId:guid}/share-link", async (Guid caseId, ClaimsPrincipal principal, PlatformCaseService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var created = await service.CreateOrRotateShareLinkAsync(caseId, actorId, ct);
            return created is null ? Results.NotFound() : Results.Ok(created);
        });

        group.MapDelete("/cases/{caseId:guid}/share-link", async (Guid caseId, ClaimsPrincipal principal, PlatformCaseService service, CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await service.RevokeShareLinkAsync(caseId, actorId, ct) ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
