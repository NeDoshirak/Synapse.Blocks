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
        return app;
    }
}
