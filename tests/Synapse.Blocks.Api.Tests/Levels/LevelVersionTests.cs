using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Synapse.Blocks.Api.Data;

namespace Synapse.Blocks.Api.Tests.Levels;

public sealed partial class TeacherLevelEndpointsTests
{
    [Fact]
    public async Task Version_snapshot_keeps_intro_media_and_test_cases_when_level_is_updated()
    {
        using var teacher = await CreateTeacherClientAsync();
        var original = Definition("Original");
        original.IntroSteps = [new() { Title = "Welcome", Body = "Read this", MediaUrl = "https://example.test/intro.gif" }];
        original.Tests = [new() { Name = "hidden regression", Input = "input", ExpectedOutput = "secret", Hidden = true }];
        var create = await teacher.PostAsJsonAsync("/api/teacher/levels", new { definition = original });
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var levelId = created.RootElement.GetProperty("id").GetGuid();
        var versionId = created.RootElement.GetProperty("currentVersionId").GetGuid();
        var updated = await teacher.PutAsJsonAsync($"/api/teacher/levels/{levelId}", new
        {
            expectedVersionId = versionId,
            definition = Definition("Edited")
        });
        Assert.Equal(System.Net.HttpStatusCode.OK, updated.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshot = await db.LevelVersions.AsNoTracking().SingleAsync(version => version.Id == versionId);
        using var snapshotJson = JsonDocument.Parse(snapshot.DefinitionJson);
        Assert.Equal("Original", snapshotJson.RootElement.GetProperty("title").GetString());
        Assert.Equal("https://example.test/intro.gif", snapshotJson.RootElement.GetProperty("introSteps")[0].GetProperty("mediaUrl").GetString());
        Assert.Equal("secret", snapshotJson.RootElement.GetProperty("tests")[0].GetProperty("expectedOutput").GetString());
        Assert.True(snapshotJson.RootElement.GetProperty("tests")[0].GetProperty("hidden").GetBoolean());
    }
}
