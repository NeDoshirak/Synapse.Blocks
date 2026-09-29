using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Synapse.Blocks.Api.Tests.Infrastructure;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Tests.Levels;

public sealed partial class TeacherLevelEndpointsTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = new(database.ConnectionString, "admin@example.test", "Admin-password-123!");

    [Fact]
    public async Task Teacher_can_create_level_and_list_only_owned_levels()
    {
        using var teacher = await CreateTeacherClientAsync();
        var create = await teacher.PostAsJsonAsync("/api/teacher/levels", new { definition = Definition("Owned level") });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var responseJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = responseJson.RootElement.GetProperty("id").GetGuid();
        var list = await teacher.GetFromJsonAsync<JsonElement[]>("/api/teacher/levels");
        Assert.Contains(list!, row => row.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Invalid_definition_is_rejected()
    {
        using var teacher = await CreateTeacherClientAsync();
        var response = await teacher.PostAsJsonAsync("/api/teacher/levels", new { definition = Definition(" ") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Another_teacher_cannot_read_or_mutate_level()
    {
        using var owner = await CreateTeacherClientAsync();
        var created = await owner.PostAsJsonAsync("/api/teacher/levels", new { definition = Definition("Private") });
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        var version = body.RootElement.GetProperty("currentVersionId").GetGuid();

        using var other = await CreateTeacherClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/teacher/levels/{id}")).StatusCode);
        var update = await other.PutAsJsonAsync($"/api/teacher/levels/{id}", new { expectedVersionId = version, definition = Definition("Hijack") });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        using var unchanged = JsonDocument.Parse(await (await owner.GetAsync($"/api/teacher/levels/{id}")).Content.ReadAsStringAsync());
        Assert.Equal("Private", unchanged.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Updating_creates_immutable_versions_and_rejects_stale_writes()
    {
        using var teacher = await CreateTeacherClientAsync();
        var created = await teacher.PostAsJsonAsync("/api/teacher/levels", new { definition = Definition("Version one") });
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        var versionOne = body.RootElement.GetProperty("currentVersionId").GetGuid();

        var updated = await teacher.PutAsJsonAsync($"/api/teacher/levels/{id}", new { expectedVersionId = versionOne, definition = Definition("Version two") });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        var versionTwo = updatedBody.RootElement.GetProperty("currentVersionId").GetGuid();
        Assert.NotEqual(versionOne, versionTwo);
        Assert.Equal(2, updatedBody.RootElement.GetProperty("version").GetInt32());

        var stale = await teacher.PutAsJsonAsync($"/api/teacher/levels/{id}", new { expectedVersionId = versionOne, definition = Definition("Stale edit") });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var versions = await teacher.GetFromJsonAsync<JsonElement[]>($"/api/teacher/levels/{id}/versions");
        Assert.Equal(new[] { 2, 1 }, versions!.Select(version => version.GetProperty("version").GetInt32()));
        using var current = JsonDocument.Parse(await (await teacher.GetAsync($"/api/teacher/levels/{id}")).Content.ReadAsStringAsync());
        Assert.Equal("Version two", current.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task First_catalog_access_copies_starter_levels_to_owner()
    {
        using var teacher = await CreateTeacherClientAsync();
        var levels = await teacher.GetFromJsonAsync<JsonElement[]>("/api/teacher/levels");
        Assert.NotEmpty(levels!);
        Assert.Contains(levels!, level => !string.Equals(level.GetProperty("title").GetString(), "", StringComparison.Ordinal));
    }

    private async Task<HttpClient> CreateTeacherClientAsync()
    {
        using var admin = _factory.CreateClient(new() { HandleCookies = true });
        var csrf = await admin.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        admin.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/auth/sign-in", new { email = "admin@example.test", password = "Admin-password-123!" })).StatusCode);
        csrf = await admin.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        admin.DefaultRequestHeaders.Remove("RequestVerificationToken");
        admin.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var email = $"teacher-{Guid.NewGuid():N}@example.test";
        var issued = await admin.PostAsJsonAsync("/api/platform/invitations", new { email });
        var invitation = await issued.Content.ReadFromJsonAsync<InvitationDto>();
        csrf = await admin.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        admin.DefaultRequestHeaders.Remove("RequestVerificationToken");
        admin.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/platform/invitations/accept", new { token = invitation!.Token, password = "Teacher-password-123!" })).StatusCode);
        var teacher = _factory.CreateClient(new() { HandleCookies = true });
        csrf = await teacher.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        teacher.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        Assert.Equal(HttpStatusCode.OK, (await teacher.PostAsJsonAsync("/api/auth/sign-in", new { email, password = "Teacher-password-123!" })).StatusCode);
        csrf = await teacher.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        teacher.DefaultRequestHeaders.Remove("RequestVerificationToken");
        teacher.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        return teacher;
    }

    private static LevelDefinition Definition(string title) => new()
    {
        Title = title,
        Tests = [new LevelTestCase { Name = "identity", Input = "1", ExpectedOutput = "1" }]
    };

    private sealed record CsrfDto(string RequestToken);
    private sealed record InvitationDto(string Token);
    public void Dispose() => _factory.Dispose();
}
