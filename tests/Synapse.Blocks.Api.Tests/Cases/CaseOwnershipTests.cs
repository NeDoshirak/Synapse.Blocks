using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Cases;

public sealed class CaseOwnershipTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Case_pins_selected_versions_in_requested_order()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var first = await CaseTestSupport.CreateLevelAsync(teacher, "First");
        var second = await CaseTestSupport.CreateLevelAsync(teacher, "Second");
        var response = await CaseTestSupport.CreateCaseAsync(teacher, "Ordered", second.VersionId, first.VersionId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("orderedLevels", json.RootElement.GetProperty("caseType").GetString());
        var levels = json.RootElement.GetProperty("levels");
        Assert.Equal(second.VersionId, levels[0].GetProperty("levelVersionId").GetGuid());
        Assert.Equal(0, levels[0].GetProperty("order").GetInt32());
        Assert.Equal(first.VersionId, levels[1].GetProperty("levelVersionId").GetGuid());
        Assert.Equal(1, levels[1].GetProperty("order").GetInt32());
    }

    [Fact]
    public async Task Empty_case_duplicate_versions_and_more_than_one_hundred_levels_are_rejected()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await CaseTestSupport.CreateCaseAsync(teacher, "Empty")).StatusCode);
        var level = await CaseTestSupport.CreateLevelAsync(teacher, "One");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await CaseTestSupport.CreateCaseAsync(teacher, "Duplicate", level.VersionId, level.VersionId)).StatusCode);
        var tooMany = Enumerable.Repeat(level.VersionId, 101).ToArray();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await CaseTestSupport.CreateCaseAsync(teacher, "Too many", tooMany)).StatusCode);
    }

    [Fact]
    public async Task Other_teacher_cannot_read_update_publish_or_issue_link_for_case()
    {
        using var owner = await TeacherTestClients.CreateAsync(_factory);
        var level = await CaseTestSupport.CreateLevelAsync(owner, "Owner level");
        var created = await CaseTestSupport.CreateCaseAsync(owner, "Private case", level.VersionId);
        var caseId = await CaseTestSupport.GetCaseIdAsync(created);

        using var other = await TeacherTestClients.CreateAsync(_factory);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/cases/{caseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/teacher/cases/{caseId}", new { caseType = "orderedLevels", title = "stolen", description = "", orderedLevelVersionIds = new[] { level.VersionId } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/teacher/cases/{caseId}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/teacher/cases/{caseId}/share-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/teacher/cases/{caseId}")).StatusCode);
        using var current = JsonDocument.Parse(await (await owner.GetAsync($"/api/teacher/cases/{caseId}")).Content.ReadAsStringAsync());
        Assert.Equal("Private case", current.RootElement.GetProperty("title").GetString());
    }

    public void Dispose() => _factory.Dispose();
}
