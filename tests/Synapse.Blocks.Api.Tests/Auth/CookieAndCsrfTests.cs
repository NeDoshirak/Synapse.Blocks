using System.Net;
using System.Net.Http.Json;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Auth;

public sealed class CookieAndCsrfTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new(database.ConnectionString);

    [Fact]
    public async Task Anonymous_cookie_mutation_requires_antiforgery_token()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { email = "student@example.test", password = "password" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_authenticated_content_mutation_requires_antiforgery_header()
    {
        using var client = _factory.CreateClient(new() { HandleCookies = true });
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new { email = "admin@example.test", password = "Admin-password-123!" });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        var response = await client.PostAsJsonAsync("/api/teacher/cases", new { caseType = "orderedLevels", title = "No token", description = "", orderedLevelVersionIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
    private sealed record CsrfDto(string RequestToken);
}
