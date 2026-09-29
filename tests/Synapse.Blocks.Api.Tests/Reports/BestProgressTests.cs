using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Cases;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Api.Tests.Students;

namespace Synapse.Blocks.Api.Tests.Reports;

public sealed class BestProgressTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Report_groups_normalized_names_and_keeps_all_equal_best_attempts()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var levelOne = await CaseTestSupport.CreateLevelAsync(teacher, StudentTestSupport.Level("One", "1", "1"));
        var levelTwo = await CaseTestSupport.CreateLevelAsync(teacher, StudentTestSupport.Level("Two", "1", "1"));
        var created = await CaseTestSupport.CreateCaseAsync(teacher, "Progress", levelOne.VersionId, levelTwo.VersionId);
        var caseId = await CaseTestSupport.GetCaseIdAsync(created);
        var token = await CaseTestSupport.CreateAndPublishShareAsync(teacher, caseId);

        await CompleteLevelsAsync(token, "Ada Lovelace", 1);
        await CompleteLevelsAsync(token, "ada   lovelace", 2);
        await CompleteLevelsAsync(token, "ADA LOVELACE", 2);

        var report = await teacher.GetFromJsonAsync<JsonElement>($"/api/teacher/cases/{caseId}/report");
        Assert.Equal(caseId, report.GetProperty("caseId").GetGuid());
        var participant = Assert.Single(report.GetProperty("participants").EnumerateArray());
        Assert.Equal("Ada Lovelace", participant.GetProperty("displayName").GetString());
        Assert.Equal(2, participant.GetProperty("bestProgress").GetInt32());
        var attempts = participant.GetProperty("attempts").EnumerateArray().ToArray();
        Assert.Equal(3, attempts.Length);
        Assert.Equal(new[] { 1, 2, 2 }, attempts.Select(attempt => attempt.GetProperty("completedLevelCount").GetInt32()));
        Assert.Equal(2, attempts.Count(attempt => attempt.GetProperty("completedLevelCount").GetInt32() == 2));
    }

    [Fact]
    public async Task Report_for_case_without_participants_is_empty()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var level = await CaseTestSupport.CreateLevelAsync(teacher, StudentTestSupport.Level("Empty report", "1", "1"));
        var created = await CaseTestSupport.CreateCaseAsync(teacher, "No students", level.VersionId);
        var id = await CaseTestSupport.GetCaseIdAsync(created);
        var report = await teacher.GetFromJsonAsync<JsonElement>($"/api/teacher/cases/{id}/report");
        Assert.Empty(report.GetProperty("participants").EnumerateArray());
    }

    private async Task CompleteLevelsAsync(string token, string name, int count)
    {
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        await StudentTestSupport.JoinAsync(student, token, name);
        var attempt = await StudentTestSupport.StartAttemptAsync(student, token);
        var caseView = await student.GetFromJsonAsync<JsonElement>($"/api/student/cases/{token}");
        var versions = caseView.GetProperty("levels").EnumerateArray().Select(level => level.GetProperty("levelVersionId").GetGuid()).ToArray();
        for (var index = 0; index < count; index++)
        {
            var route = $"/api/student/cases/{token}/attempts/{attempt}/levels/{versions[index]}/evaluate";
            var result = await student.PostAsJsonAsync(route, StudentTestSupport.IdentityProgram());
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("passed").GetBoolean());
        }
    }

    public void Dispose() => _factory.Dispose();
}
