using System.Net;
using Synapse.Blocks.Api.Tests.Cases;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Api.Tests.Students;

namespace Synapse.Blocks.Api.Tests.Reports;

public sealed class ReportOwnershipTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Other_teacher_cannot_read_case_report()
    {
        using var owner = await TeacherTestClients.CreateAsync(_factory);
        var level = await CaseTestSupport.CreateLevelAsync(owner, StudentTestSupport.Level("Report owner", "1", "1"));
        var created = await CaseTestSupport.CreateCaseAsync(owner, "Private report", level.VersionId);
        var caseId = await CaseTestSupport.GetCaseIdAsync(created);
        using var other = await TeacherTestClients.CreateAsync(_factory);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/cases/{caseId}/report")).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
