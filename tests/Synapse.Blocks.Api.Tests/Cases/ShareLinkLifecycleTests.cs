using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Cases;

public sealed class ShareLinkLifecycleTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Link_is_unpredictable_hashed_rotatable_and_public_view_hides_hidden_tests()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var level = await CaseTestSupport.CreateLevelAsync(teacher, "Public level");
        var created = await CaseTestSupport.CreateCaseAsync(teacher, "Public case", level.VersionId);
        var caseId = await CaseTestSupport.GetCaseIdAsync(created);
        await teacher.PostAsync($"/api/teacher/cases/{caseId}/publish", null);
        var first = await teacher.PostAsync($"/api/teacher/cases/{caseId}/share-link", null);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var firstToken = firstJson.RootElement.GetProperty("token").GetString()!;
        Assert.True(firstToken.Length >= 43);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedHash = await db.ShareLinks.Select(link => link.TokenHash).SingleAsync();
            Assert.NotEqual(firstToken, storedHash);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(firstToken))), storedHash);
        }
        var studentView = await teacher.GetFromJsonAsync<JsonElement>($"/api/student/cases/{firstToken}");
        var tests = studentView.GetProperty("levels")[0].GetProperty("definition").GetProperty("tests");
        Assert.Single(tests.EnumerateArray());
        Assert.Equal("public", tests[0].GetProperty("name").GetString());
        Assert.DoesNotContain("secret", studentView.ToString(), StringComparison.Ordinal);
        using var details = JsonDocument.Parse(await (await teacher.GetAsync($"/api/teacher/cases/{caseId}")).Content.ReadAsStringAsync());
        Assert.DoesNotContain("token", details.ToString(), StringComparison.OrdinalIgnoreCase);

        var rotate = await teacher.PostAsync($"/api/teacher/cases/{caseId}/share-link", null);
        using var rotateJson = JsonDocument.Parse(await rotate.Content.ReadAsStringAsync());
        var secondToken = rotateJson.RootElement.GetProperty("token").GetString()!;
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/student/cases/{firstToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/student/cases/not-a-real-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/cases/{caseId}/share-link")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/student/cases/{secondToken}")).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
