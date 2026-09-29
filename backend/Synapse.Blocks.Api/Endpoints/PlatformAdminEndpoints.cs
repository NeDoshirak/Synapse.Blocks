using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Synapse.Blocks.Api.Auth;

namespace Synapse.Blocks.Api.Endpoints;

public static class PlatformAdminEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/platform").RequireAuthorization(policy => policy.RequireRole(BootstrapPlatformAdmin.PlatformAdminRole));
        group.MapPost("/invitations", async (InvitationRequest request, ClaimsPrincipal principal, HttpContext context, IAntiforgery antiforgery, InvitationService invitations) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(request.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email address is required."] });
            var inviterId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
            var token = await invitations.CreateAsync(request.Email, expiresAt, inviterId);
            var origin = (context.RequestServices.GetRequiredService<IConfiguration>()["PublicOrigin"] ?? "http://localhost:8080").TrimEnd('/');
            var invitationUrl = $"{origin}/admin/invitations/accept?token={Uri.EscapeDataString(token)}";
            return Results.Created("/api/platform/invitations/accept", new { token, expiresAt, invitationUrl });
        });
        app.MapPost("/api/platform/invitations/accept", async (AcceptInvitationRequest request, HttpContext context, IAntiforgery antiforgery, InvitationService invitations) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            return await invitations.AcceptAsync(request.Token, request.Password)
                ? Results.Ok()
                : Results.BadRequest(new { error = "Invitation is invalid, expired, already used, or password is invalid." });
        }).AllowAnonymous();
        return app;
    }

    public sealed record InvitationRequest(string Email);
    public sealed record AcceptInvitationRequest(string Token, string Password);
}
