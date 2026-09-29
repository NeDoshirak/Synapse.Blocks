using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Synapse.Blocks.Api.Auth;

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

        app.MapPost("/api/auth/sign-in", async (SignInRequest request, HttpContext context, IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var result = await signIn.PasswordSignInAsync(request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok() : Results.Unauthorized();
        }).AllowAnonymous();

        app.MapPost("/api/auth/sign-out", async (HttpContext context, IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            return user is null ? Results.Unauthorized() : Results.Ok(new
            {
                userId = user.Id,
                email = user.Email,
                roles = await users.GetRolesAsync(user)
            });
        }).RequireAuthorization();
        return app;
    }

    public sealed record SignInRequest(string Email, string Password);
}
