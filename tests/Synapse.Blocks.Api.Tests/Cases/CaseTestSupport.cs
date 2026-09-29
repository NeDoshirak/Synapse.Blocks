using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Tests.Cases;

internal static class CaseTestSupport
{
    public static async Task<(Guid Id, Guid VersionId)> CreateLevelAsync(HttpClient client, string title)
    {
        var result = await client.PostAsJsonAsync("/api/teacher/levels", new { definition = new LevelDefinition { Title = title, Tests = [new LevelTestCase { Name = "public", Input = "1", ExpectedOutput = "1" }, new LevelTestCase { Name = "hidden", Input = "2", ExpectedOutput = "secret", Hidden = true }] } });
        result.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("currentVersionId").GetGuid());
    }

    public static Task<HttpResponseMessage> CreateCaseAsync(HttpClient client, string title, params Guid[] versionIds)
        => client.PostAsJsonAsync("/api/teacher/cases", new { caseType = "orderedLevels", title, description = "A classroom case", orderedLevelVersionIds = versionIds });

    public static async Task<Guid> GetCaseIdAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<string> CreateAndPublishShareAsync(HttpClient client, Guid caseId)
    {
        var publish = await client.PostAsync($"/api/teacher/cases/{caseId}/publish", null);
        publish.EnsureSuccessStatusCode();
        var share = await client.PostAsync($"/api/teacher/cases/{caseId}/share-link", null);
        share.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await share.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }

    public static ApiWebApplicationFactory CreateFactory(PostgreSqlFixture database)
        => new(database.ConnectionString, TeacherTestClients.AdminEmail, TeacherTestClients.AdminPassword);
}
