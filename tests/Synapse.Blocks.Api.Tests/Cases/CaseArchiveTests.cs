using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Api.Tests.Students;

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
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        await StudentTestSupport.JoinAsync(student, token, "Archived student");
        var attemptId = await StudentTestSupport.StartAttemptAsync(student, token);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teacher/cases/{caseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/student/cases/{token}")).StatusCode);
        using var archived = System.Text.Json.JsonDocument.Parse(await (await teacher.GetAsync($"/api/teacher/cases/{caseId}")).Content.ReadAsStringAsync());
        Assert.True(archived.RootElement.GetProperty("archived").GetBoolean());
        Assert.Single(archived.RootElement.GetProperty("levels").EnumerateArray());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var preservedAttempt = await db.Attempts.AsNoTracking().SingleAsync(attempt => attempt.Id == attemptId);
        Assert.Equal("InProgress", preservedAttempt.Status);
    }

    public void Dispose() => _factory.Dispose();
}
