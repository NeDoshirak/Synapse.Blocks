using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Auth;

public sealed class InvitationFlowTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new(database.ConnectionString, bootstrapEmail: "admin@example.test", bootstrapPassword: "Admin-password-123!");

    [Fact]
    public async Task Platform_admin_can_issue_and_accept_single_use_invitation()
    {
        using var client = _factory.CreateClient(new() { HandleCookies = true });
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var admin = await client.PostAsJsonAsync("/api/auth/sign-in", new { email = "admin@example.test", password = "Admin-password-123!" });
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        var cookieOptions = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme).Cookie;
        Assert.True(cookieOptions.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookieOptions.SameSite);
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.Equal("admin@example.test", current!.Email);
        Assert.Contains("PlatformAdmin", current.Roles);
        csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);

        var invitation = await client.PostAsJsonAsync("/api/platform/invitations", new { email = "teacher@example.test" });
        Assert.True(invitation.StatusCode == HttpStatusCode.Created, await invitation.Content.ReadAsStringAsync());
        var token = (await invitation.Content.ReadFromJsonAsync<InvitationDto>())!.Token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedHash = await db.Invitations.Where(item => item.Email == "teacher@example.test").Select(item => item.TokenHash).SingleAsync();
            Assert.NotEqual(token, storedHash);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), storedHash);
        }

        var accept = await client.PostAsJsonAsync("/api/platform/invitations/accept", new { token, password = "Teacher-password-123!" });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var reuse = await client.PostAsJsonAsync("/api/platform/invitations/accept", new { token, password = "Teacher-password-123!" });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        using var teacherClient = _factory.CreateClient(new() { HandleCookies = true });
        var teacherCsrf = await teacherClient.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        teacherClient.DefaultRequestHeaders.Add("RequestVerificationToken", teacherCsrf!.RequestToken);
        var teacherLogin = await teacherClient.PostAsJsonAsync("/api/auth/sign-in", new { email = "teacher@example.test", password = "Teacher-password-123!" });
        Assert.Equal(HttpStatusCode.OK, teacherLogin.StatusCode);
        teacherCsrf = await teacherClient.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        teacherClient.DefaultRequestHeaders.Remove("RequestVerificationToken");
        teacherClient.DefaultRequestHeaders.Add("RequestVerificationToken", teacherCsrf!.RequestToken);
        var deniedInvitation = await teacherClient.PostAsJsonAsync("/api/platform/invitations", new { email = "other@example.test" });
        Assert.Equal(HttpStatusCode.Forbidden, deniedInvitation.StatusCode);
    }

    [Fact]
    public async Task Expired_invitation_is_rejected()
    {
        using var client = _factory.CreateClient(new() { HandleCookies = true });
        _ = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        using var scope = _factory.Services.CreateScope();
        var invitations = scope.ServiceProvider.GetRequiredService<InvitationService>();
        var token = await invitations.CreateAsync("expired@example.test", DateTimeOffset.UtcNow.AddMinutes(-1), "admin");
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var response = await client.PostAsJsonAsync("/api/platform/invitations/accept", new { token, password = "Teacher-password-123!" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_for_existing_email_cannot_create_a_second_account()
    {
        using var client = _factory.CreateClient(new() { HandleCookies = true });
        using var scope = _factory.Services.CreateScope();
        var invitations = scope.ServiceProvider.GetRequiredService<InvitationService>();
        var token = await invitations.CreateAsync("admin@example.test", DateTimeOffset.UtcNow.AddDays(1), "admin");
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var response = await client.PostAsJsonAsync("/api/platform/invitations/accept", new { token, password = "Teacher-password-123!" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
    private sealed record CsrfDto(string RequestToken);
    private sealed record InvitationDto(string Token);
    private sealed record CurrentUserDto(string UserId, string Email, string[] Roles);
}
