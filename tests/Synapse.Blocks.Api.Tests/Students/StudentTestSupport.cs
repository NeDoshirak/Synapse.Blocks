using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Cases;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Tests.Students;

internal static class StudentTestSupport
{
    public static async Task<(string Token, Guid[] Versions)> CreatePublishedCaseAsync(HttpClient teacher, params LevelDefinition[] definitions)
    {
        var levels = new List<(Guid Id, Guid VersionId)>();
        foreach (var definition in definitions) levels.Add(await CaseTestSupport.CreateLevelAsync(teacher, definition));
        var created = await CaseTestSupport.CreateCaseAsync(teacher, "Student case", levels.Select(level => level.VersionId).ToArray());
        var id = await CaseTestSupport.GetCaseIdAsync(created);
        var token = await CaseTestSupport.CreateAndPublishShareAsync(teacher, id);
        return (token, levels.Select(level => level.VersionId).ToArray());
    }

    public static async Task<HttpClient> CreateStudentClientAsync(ApiWebApplicationFactory factory)
    {
        var student = factory.CreateClient(new() { HandleCookies = true });
        var csrf = await student.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        student.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        return student;
    }

    public static async Task<Guid> JoinAsync(HttpClient student, string token, string name)
    {
        var response = await student.PostAsJsonAsync($"/api/student/cases/{token}/participants", new { displayName = name });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("participantId").GetGuid();
    }

    public static async Task<Guid> StartAttemptAsync(HttpClient student, string token)
    {
        var response = await student.PostAsync($"/api/student/cases/{token}/attempts", null);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("attemptId").GetGuid();
    }

    public static BlockProgram IdentityProgram()
    {
        var input = new BlockNode { Kind = BlockKind.Input };
        var output = new BlockNode { Kind = BlockKind.Output };
        return new BlockProgram { Nodes = [input, output], Connections = [new BlockConnection { FromNodeId = input.Id, ToNodeId = output.Id }] };
    }

    public static LevelDefinition Level(string title, string input, string expected, bool hidden = false) => new()
    {
        Title = title,
        Tests = [new LevelTestCase { Name = hidden ? "secret-check" : "public-check", Input = input, ExpectedOutput = expected, Hidden = hidden }]
    };

    private sealed record CsrfDto(string RequestToken);
}
