using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Contracts.Auth;

namespace Synapse.Blocks.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { requestToken = tokens.RequestToken });
        }).AllowAnonymous();

        app.MapPost("/api/auth/sign-in", async (
            SignInRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();

            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive) return Results.Unauthorized();

            var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
            if (result.RequiresTwoFactor) return Results.Ok(new AuthStepDto("twoFactor"));
            if (!result.Succeeded) return Results.Unauthorized();
            return Results.Ok(new AuthStepDto(user.TwoFactorEnabled ? "authenticated" : "authenticatorSetup"));
        })
            .AllowAnonymous()
            .RequireRateLimiting("auth-attempts");

        app.MapPost("/api/auth/two-factor/verify", async (
            AuthenticatorCodeRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            if (!AuthenticatorService.IsAuthenticatorCode(request.Code)) return Results.Unauthorized();
            var result = await signIn.TwoFactorAuthenticatorSignInAsync(request.Code.Trim(), isPersistent: false, rememberClient: false);
            return result.Succeeded
                ? Results.Ok(new AuthStepDto("authenticated"))
                : Results.Unauthorized();
        })
            .AllowAnonymous()
            .RequireRateLimiting("auth-attempts");

        app.MapPost("/api/auth/recovery-codes/sign-in", async (
            RecoveryCodeSignInRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(request.Code)) return Results.Unauthorized();
            var result = await signIn.TwoFactorRecoveryCodeSignInAsync(request.Code.Trim());
            return result.Succeeded
                ? Results.Ok(new AuthStepDto("authenticated"))
                : Results.Unauthorized();
        })
            .AllowAnonymous()
            .RequireRateLimiting("auth-attempts");

        app.MapGet("/api/auth/authenticator/setup", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            AuthenticatorService authenticator) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var setup = await authenticator.GetSetupAsync(user);
            return setup is null ? Results.Conflict() : Results.Ok(setup);
        }).RequireAuthorization();

        app.MapPost("/api/auth/authenticator/confirm", async (
            AuthenticatorCodeRequest request,
            ClaimsPrincipal principal,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<ApplicationUser> users,
            AuthenticatorService authenticator) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var user = await users.GetUserAsync(principal);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var recoveryCodes = await authenticator.ConfirmAsync(user, request.Code);
            return recoveryCodes is null
                ? Results.BadRequest(new { error = "The authenticator code is invalid or setup is no longer available." })
                : Results.Ok(new RecoveryCodesDto(recoveryCodes));
        })
            .RequireAuthorization()
            .RequireRateLimiting("auth-attempts");

        app.MapPost("/api/auth/recovery-codes/regenerate", async (
            ClaimsPrincipal principal,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<ApplicationUser> users,
            AuthenticatorService authenticator) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var user = await users.GetUserAsync(principal);
            if (user is null || !user.IsActive || !user.TwoFactorEnabled) return Results.Unauthorized();
            return Results.Ok(new RecoveryCodesDto(await authenticator.RegenerateRecoveryCodesAsync(user)));
        })
            .RequireAuthorization("ActiveMfa")
            .RequireRateLimiting("auth-attempts");

        app.MapPost("/api/auth/password-reset/complete", async (
            PasswordResetCompleteRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            UserManager<ApplicationUser> users) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrEmpty(request.NewPassword))
                return Results.BadRequest(new { error = "The password reset link or password is invalid." });

            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.BadRequest(new { error = "The password reset link or password is invalid." });

            var reset = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!reset.Succeeded) return Results.BadRequest(new { error = "The password reset link or password is invalid." });
            await users.UpdateSecurityStampAsync(user);
            return Results.NoContent();
        })
            .AllowAnonymous()
            .RequireRateLimiting("auth-attempts");

        app.MapPost("/api/auth/sign-out", async (HttpContext context, IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            if (!user.TwoFactorEnabled)
                return Results.Ok(new { userId = user.Id, twoFactorEnabled = false });

            return Results.Ok(new
            {
                userId = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                displayName = user.DisplayName,
                teacherDescription = user.TeacherDescription,
                isActive = user.IsActive,
                twoFactorEnabled = user.TwoFactorEnabled,
                roles = await users.GetRolesAsync(user)
            });
        }).RequireAuthorization();
        return app;
    }

    public sealed record SignInRequest(string Email, string Password);
}
