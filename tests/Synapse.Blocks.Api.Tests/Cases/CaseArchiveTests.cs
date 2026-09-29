using System.Net;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Cases;

public sealed class CaseArchiveTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Archiving_keeps_case_and_levels_but_closes_public_link()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var level = await CaseTestSupport.CreateLevelAsync(teacher, "Archived level");
        var caseResponse = await CaseTestSupport.CreateCaseAsync(teacher, "Archived case", level.VersionId);
        var caseId = await CaseTestSupport.GetCaseIdAsync(caseResponse);
        var token = await CaseTestSupport.CreateAndPublishShareAsync(teacher, caseId);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync($"/api/student/cases/{token}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/cases/{caseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/student/cases/{token}")).StatusCode);
        using var archived = System.Text.Json.JsonDocument.Parse(await (await teacher.GetAsync($"/api/teacher/cases/{caseId}")).Content.ReadAsStringAsync());
        Assert.True(archived.RootElement.GetProperty("archived").GetBoolean());
        Assert.Single(archived.RootElement.GetProperty("levels").EnumerateArray());
    }

    public void Dispose() => _factory.Dispose();
}
