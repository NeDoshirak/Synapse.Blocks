using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Tests.Cases;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Tests.Students;

public sealed class AttemptLifecycleTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Name_is_required_normalized_and_same_device_continues_same_participant()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var (token, _) = await StudentTestSupport.CreatePublishedCaseAsync(teacher, StudentTestSupport.Level("Identity", "1", "1"));
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await student.PostAsJsonAsync($"/api/student/cases/{token}/participants", new { displayName = "  " })).StatusCode);
        var first = await student.PostAsJsonAsync($"/api/student/cases/{token}/participants", new { displayName = "  Ada   Lovelace " });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var participantId = firstJson.RootElement.GetProperty("participantId").GetGuid();
        Assert.Equal("Ada Lovelace", firstJson.RootElement.GetProperty("displayName").GetString());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var continuationHash = await db.Participants.Where(item => item.Id == participantId).Select(item => item.ContinuationHash).SingleAsync();
            Assert.Equal(64, continuationHash.Length);
        }
        var repeat = await student.PostAsJsonAsync($"/api/student/cases/{token}/participants", new { displayName = "ada lovelace" });
        using var repeatJson = JsonDocument.Parse(await repeat.Content.ReadAsStringAsync());
        Assert.Equal(participantId, repeatJson.RootElement.GetProperty("participantId").GetGuid());

        var firstAttempt = await StudentTestSupport.StartAttemptAsync(student, token);
        var secondAttempt = await StudentTestSupport.StartAttemptAsync(student, token);
        Assert.NotEqual(firstAttempt, secondAttempt);
        var (otherToken, otherVersions) = await StudentTestSupport.CreatePublishedCaseAsync(teacher, StudentTestSupport.Level("Other", "1", "1"));
        Assert.Equal(HttpStatusCode.NotFound, (await student.PostAsJsonAsync($"/api/student/cases/{otherToken}/attempts/{secondAttempt}/levels/{otherVersions[0]}/evaluate", StudentTestSupport.IdentityProgram())).StatusCode);
        var current = await student.GetFromJsonAsync<JsonElement>($"/api/student/cases/{token}/attempts/current");
        Assert.Equal(secondAttempt, current.GetProperty("attemptId").GetGuid());
    }

    [Fact]
    public async Task Name_must_not_exceed_eighty_characters_after_normalization()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var (token, _) = await StudentTestSupport.CreatePublishedCaseAsync(teacher, StudentTestSupport.Level("Identity", "1", "1"));
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        var response = await student.PostAsJsonAsync($"/api/student/cases/{token}/participants", new { displayName = new string('A', 81) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
