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

    public void Dispose() => _factory.Dispose();
}
