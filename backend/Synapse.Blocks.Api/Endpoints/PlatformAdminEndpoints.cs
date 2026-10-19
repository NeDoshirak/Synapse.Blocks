using System.Security.Claims;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.Auth;

namespace Synapse.Blocks.Api.Endpoints;

public static class PlatformAdminEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/platform")
            .RequireAuthorization(policy => policy.RequireRole(BootstrapPlatformAdmin.PlatformAdminRole))
            .RequireAuthorization("ActiveMfa")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapGet("/teachers", async (string? search, TeacherAccountService accounts, CancellationToken ct) =>
            Results.Ok(await accounts.ListTeachersAsync(search, ct)));

        group.MapPost("/invitations", async (
            InvitationRequest request,
            ClaimsPrincipal principal,
            TeacherAccountService accounts,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email address is required."] });

            var inviterId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var created = await accounts.CreateInvitationAsync(request.Email, inviterId, ct);
            return created is null
                ? Results.Conflict(new { error = "A teacher account or active invitation already exists for this email." })
                : Results.Created("/api/platform/invitations", created);
        });

        group.MapGet("/invitations", async (TeacherAccountService accounts, CancellationToken ct) =>
            Results.Ok(await accounts.ListPendingInvitationsAsync(ct)));

        group.MapPost("/invitations/{invitationId:guid}/revoke", async (
            Guid invitationId,
            ClaimsPrincipal principal,
            TeacherAccountService accounts,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await accounts.RevokeInvitationAsync(invitationId, actorId, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapPost("/teachers/{teacherId}/block", async (
            string teacherId,
            ClaimsPrincipal principal,
            TeacherAccountService accounts,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await accounts.SetTeacherActiveAsync(teacherId, false, actorId, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapPost("/teachers/{teacherId}/reactivate", async (
            string teacherId,
            ClaimsPrincipal principal,
            TeacherAccountService accounts,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await accounts.SetTeacherActiveAsync(teacherId, true, actorId, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapPost("/teachers/{teacherId}/password-reset", async (
            string teacherId,
            ClaimsPrincipal principal,
            TeacherAccountService accounts,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var reset = await accounts.CreatePasswordResetLinkAsync(teacherId, actorId, ct);
            return reset is null ? Results.NotFound() : Results.Ok(reset);
        });

        group.MapPost("/teachers/{teacherId}/two-factor/reset", async (
            string teacherId,
            ClaimsPrincipal principal,
            TeacherAccountService accounts,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await accounts.ResetTeacherTwoFactorAsync(teacherId, actorId, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        app.MapPost("/api/platform/invitations/accept", async (
            AcceptInvitationRequest request,
            InvitationService invitations) =>
        {
            return await invitations.AcceptAsync(
                request.Token,
                request.Password,
                request.FirstName,
                request.LastName,
                request.TeacherDescription)
                ? Results.Ok()
                : Results.BadRequest(new { error = "Invitation is invalid, expired, already used, or the submitted details are invalid." });
        })
            .AllowAnonymous()
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }
}
