using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Cases;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Students;

public sealed class ProgressMonotonicityTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Attempt_cannot_skip_levels_and_completed_levels_remain_completed()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var (token, versions) = await StudentTestSupport.CreatePublishedCaseAsync(teacher,
            StudentTestSupport.Level("One", "1", "1"), StudentTestSupport.Level("Two", "1", "1"));
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        await StudentTestSupport.JoinAsync(student, token, "Student Three");
        var attempt = await StudentTestSupport.StartAttemptAsync(student, token);

        var routeSecond = $"/api/student/cases/{token}/attempts/{attempt}/levels/{versions[1]}/evaluate";
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync(routeSecond, StudentTestSupport.IdentityProgram())).StatusCode);
        var routeFirst = $"/api/student/cases/{token}/attempts/{attempt}/levels/{versions[0]}/evaluate";
        var passed = await student.PostAsJsonAsync(routeFirst, StudentTestSupport.IdentityProgram());
        using var result = JsonDocument.Parse(await passed.Content.ReadAsStringAsync());
        Assert.True(result.RootElement.GetProperty("passed").GetBoolean());

        var failProgram = StudentTestSupport.IdentityProgram();
        var operation = new Synapse.Blocks.Models.BlockNode { Kind = Synapse.Blocks.Models.BlockKind.Operation, Config = "+ 1" };
        failProgram.Nodes.Insert(1, operation);
        failProgram.Connections[0].ToNodeId = operation.Id;
        failProgram.Connections.Add(new() { FromNodeId = operation.Id, ToNodeId = failProgram.Nodes[2].Id });
        using var failedResubmit = JsonDocument.Parse(await (await student.PostAsJsonAsync(routeFirst, failProgram)).Content.ReadAsStringAsync());
        Assert.False(failedResubmit.RootElement.GetProperty("passed").GetBoolean());
        var current = await student.GetFromJsonAsync<JsonElement>($"/api/student/cases/{token}/attempts/current");
        Assert.Equal(1, current.GetProperty("completedLevelCount").GetInt32());
        Assert.Equal("InProgress", current.GetProperty("status").GetString());

        var next = await student.PostAsJsonAsync(routeSecond, StudentTestSupport.IdentityProgram());
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        current = await student.GetFromJsonAsync<JsonElement>($"/api/student/cases/{token}/attempts/current");
        Assert.Equal(2, current.GetProperty("completedLevelCount").GetInt32());
        Assert.Equal("Completed", current.GetProperty("status").GetString());
    }

    public void Dispose() => _factory.Dispose();
}
