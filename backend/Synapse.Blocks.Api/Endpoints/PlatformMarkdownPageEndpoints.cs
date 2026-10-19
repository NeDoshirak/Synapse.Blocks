using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.MarkdownPages;
using Synapse.Blocks.Api.Operations;

namespace Synapse.Blocks.Api.Endpoints;

public static class PlatformMarkdownPageEndpoints
{
    public static IEndpointRouteBuilder MapPlatformMarkdownPageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/platform")
            .RequireAuthorization(policy => policy.RequireRole(BootstrapPlatformAdmin.PlatformAdminRole))
            .RequireAuthorization("ActiveMfa")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .MapGet("/teachers/{teacherId}/markdown-pages", async (
                string teacherId,
                MarkdownPageService service,
                CancellationToken ct) =>
            {
                var pages = await service.ListForPlatformAsync(teacherId, ct);
                return pages is null ? Results.NotFound() : Results.Ok(pages);
            });

        return app;
    }
}
