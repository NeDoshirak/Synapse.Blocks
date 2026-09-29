using Microsoft.AspNetCore.Mvc;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.Levels;
using Synapse.Blocks.Api.Levels;

namespace Synapse.Blocks.Api.Endpoints;

public static class TeacherLevelEndpoints
{
    public static IEndpointRouteBuilder MapTeacherLevelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teacher/levels").RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapGet("/", async (ICurrentUser current, LevelService levels, CancellationToken ct) =>
            Results.Ok(await levels.ListAsync(current.UserId, ct)));
        group.MapPost("/", async (CreateLevelRequest request, ICurrentUser current, LevelService levels, CancellationToken ct) =>
        {
            try
            {
                var created = await levels.CreateAsync(current.UserId, request.Definition, ct);
                return Results.Created($"/api/teacher/levels/{created.Id}", created);
            }
            catch (LevelDefinitionValidationException ex)
            {
                return Results.UnprocessableEntity(new { errors = ex.Errors });
            }
        }).WithMetadata(new RequestSizeLimitAttribute(LevelDefinitionValidator.MaxSerializedBytes));
        group.MapGet("/{levelId:guid}", async (Guid levelId, ICurrentUser current, LevelService levels, CancellationToken ct) =>
        {
            var level = await levels.GetAsync(current.UserId, levelId, ct);
            return level is null ? Results.NotFound() : Results.Ok(level);
        });
        group.MapPut("/{levelId:guid}", async (Guid levelId, UpdateLevelRequest request, ICurrentUser current, LevelService levels, CancellationToken ct) =>
        {
            try
            {
                var updated = await levels.UpdateAsync(current.UserId, levelId, request.ExpectedVersionId, request.Definition, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }
            catch (LevelVersionConflictException)
            {
                return Results.Conflict(new { error = "The level changed. Reload it before saving." });
            }
            catch (LevelDefinitionValidationException ex)
            {
                return Results.UnprocessableEntity(new { errors = ex.Errors });
            }
        }).WithMetadata(new RequestSizeLimitAttribute(LevelDefinitionValidator.MaxSerializedBytes));
        group.MapGet("/{levelId:guid}/versions", async (Guid levelId, ICurrentUser current, LevelService levels, CancellationToken ct) =>
        {
            var versions = await levels.GetVersionsAsync(current.UserId, levelId, ct);
            return versions is null ? Results.NotFound() : Results.Ok(versions);
        });
        return app;
    }
}
