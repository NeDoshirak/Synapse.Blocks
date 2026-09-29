using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Cases;
using Synapse.Blocks.Api.Tests.Infrastructure;

namespace Synapse.Blocks.Api.Tests.Students;

public sealed class HiddenTestPrivacyTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = CaseTestSupport.CreateFactory(database);

    [Fact]
    public async Task Hidden_expected_output_is_never_returned_and_hidden_failure_does_not_complete_level()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var definition = StudentTestSupport.Level("Privacy", "1", "1");
        definition.Tests.Add(new() { Name = "private regression", Input = "2", ExpectedOutput = "secret-hidden-output", Hidden = true });
        var (token, versions) = await StudentTestSupport.CreatePublishedCaseAsync(teacher, definition);
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        await StudentTestSupport.JoinAsync(student, token, "Student One");
        var attemptId = await StudentTestSupport.StartAttemptAsync(student, token);
        while (_factory.CapturedLogs.TryDequeue(out _)) { }

        var response = await student.PostAsJsonAsync($"/api/student/cases/{token}/attempts/{attemptId}/levels/{versions[0]}/evaluate", StudentTestSupport.IdentityProgram());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-hidden-output", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-hidden-output", string.Join('\n', _factory.CapturedLogs), StringComparison.Ordinal);
        using var json = JsonDocument.Parse(payload);
        Assert.False(json.RootElement.GetProperty("passed").GetBoolean());
        Assert.Single(json.RootElement.GetProperty("publicTestResults").EnumerateArray());
        var current = await student.GetFromJsonAsync<JsonElement>($"/api/student/cases/{token}/attempts/current");
        Assert.Equal(0, current.GetProperty("completedLevelCount").GetInt32());
        Assert.Equal("InProgress", current.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Oversized_evaluation_and_program_graph_limits_are_rejected_before_running()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var (token, versions) = await StudentTestSupport.CreatePublishedCaseAsync(teacher, StudentTestSupport.Level("Limits", "1", "1"));
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        await StudentTestSupport.JoinAsync(student, token, "Student Two");
        var attempt = await StudentTestSupport.StartAttemptAsync(student, token);
        var route = $"/api/student/cases/{token}/attempts/{attempt}/levels/{versions[0]}/evaluate";

        var oversized = StudentTestSupport.IdentityProgram();
        oversized.Nodes[1].Config = new string('x', 300 * 1024);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await student.PostAsJsonAsync(route, oversized)).StatusCode);
        var tooManyNodes = StudentTestSupport.IdentityProgram();
        tooManyNodes.Nodes.AddRange(Enumerable.Range(0, 499).Select(_ => new Synapse.Blocks.Models.BlockNode()));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await student.PostAsJsonAsync(route, tooManyNodes)).StatusCode);
        var tooManyConnections = StudentTestSupport.IdentityProgram();
        var input = tooManyConnections.Nodes[0].Id;
        var output = tooManyConnections.Nodes[1].Id;
        tooManyConnections.Connections.AddRange(Enumerable.Range(0, 1000).Select(_ => new Synapse.Blocks.Models.BlockConnection { FromNodeId = input, ToNodeId = output }));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await student.PostAsJsonAsync(route, tooManyConnections)).StatusCode);
    }

    [Fact]
    public async Task Failed_public_test_does_not_complete_level()
    {
        using var teacher = await TeacherTestClients.CreateAsync(_factory);
        var (token, versions) = await StudentTestSupport.CreatePublishedCaseAsync(teacher, StudentTestSupport.Level("Public failure", "1", "1"));
        using var student = await StudentTestSupport.CreateStudentClientAsync(_factory);
        await StudentTestSupport.JoinAsync(student, token, "Student Four");
        var attempt = await StudentTestSupport.StartAttemptAsync(student, token);
        var incorrect = StudentTestSupport.IdentityProgram();
        var operation = new Synapse.Blocks.Models.BlockNode { Kind = Synapse.Blocks.Models.BlockKind.Operation, Config = "+ 1" };
        incorrect.Nodes.Insert(1, operation);
        incorrect.Connections[0].ToNodeId = operation.Id;
        incorrect.Connections.Add(new() { FromNodeId = operation.Id, ToNodeId = incorrect.Nodes[2].Id });
        var response = await student.PostAsJsonAsync($"/api/student/cases/{token}/attempts/{attempt}/levels/{versions[0]}/evaluate", incorrect);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(result.RootElement.GetProperty("passed").GetBoolean());
        var current = await student.GetFromJsonAsync<JsonElement>($"/api/student/cases/{token}/attempts/current");
        Assert.Equal(0, current.GetProperty("completedLevelCount").GetInt32());
    }

    public void Dispose() => _factory.Dispose();
}
