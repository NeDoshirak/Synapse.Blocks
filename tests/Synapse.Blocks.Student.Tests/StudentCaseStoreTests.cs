using System.Net;
using System.Text;
using Synapse.Blocks.Services;

namespace Synapse.Blocks.Student.Tests;

public class StudentCaseStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_published_case_and_reports_unavailable_token()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, """{"id":"00000000-0000-0000-0000-000000000001","caseType":"orderedLevels","title":"Loops","levels":[]}"""), _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var store = new StudentCaseStore(new HttpClient(handler) { BaseAddress = new Uri("https://school.test/") });
        Assert.Equal("Loops", (await store.LoadAsync("active-token")).Title);
        await Assert.ThrowsAsync<HttpRequestException>(() => store.LoadAsync("revoked-token"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    [InlineData("This name is much too long................................................................")]
    public async Task StartAsync_rejects_blank_or_oversized_names_before_network(string name)
    {
        var handler = new QueueHandler();
        var store = new StudentCaseStore(new HttpClient(handler) { BaseAddress = new Uri("https://school.test/") });
        await Assert.ThrowsAsync<ArgumentException>(() => store.StartAsync("case-token", name));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task StartAsync_posts_display_name_and_attempt_with_csrf_and_cookie_credentials()
    {
        var handler = new QueueHandler(
            _ => Json(HttpStatusCode.OK, """{"requestToken":"csrf"}"""),
            _ => Json(HttpStatusCode.Created, """{"participantId":"00000000-0000-0000-0000-000000000003","displayName":"  Ada   Lovelace "}"""),
            _ => Json(HttpStatusCode.Created, """{"attemptId":"00000000-0000-0000-0000-000000000001","status":"InProgress","startedAt":"2026-09-30T00:00:00Z","completedLevelCount":0}"""));
        var store = new StudentCaseStore(new HttpClient(handler) { BaseAddress = new Uri("https://school.test/") });
        var attempt = await store.StartAsync("share-token", "  Ada   Lovelace ");
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), attempt.AttemptId);
        Assert.Contains("Ada   Lovelace", handler.Requests[1].Body);
        Assert.Equal("csrf", handler.Requests[1].RequestVerificationToken);
        Assert.All(handler.Requests, request => Assert.Contains(request.Options, key => key.Contains("credentials", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ResumeAsync_returns_unfinished_attempt_or_null_when_none_exists()
    {
        var handler = new QueueHandler(_ => Json(HttpStatusCode.OK, """{"attemptId":"00000000-0000-0000-0000-000000000002","status":"InProgress","startedAt":"2026-09-30T00:00:00Z","completedLevelCount":1}"""), _ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var store = new StudentCaseStore(new HttpClient(handler) { BaseAddress = new Uri("https://school.test/") });
        Assert.Equal(1, (await store.ResumeAsync("token"))!.CompletedLevelCount);
        Assert.Null(await store.ResumeAsync("token"));
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string value) => new(code) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class QueueHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);
        public List<CapturedRequest> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.RequestUri!.ToString(), body, request.Headers.TryGetValues("RequestVerificationToken", out var values) ? values.Single() : null, request.Options.Select(option => option.Key).ToArray()));
            return _responses.Count == 0 ? new HttpResponseMessage(HttpStatusCode.NotFound) : _responses.Dequeue()(request);
        }
    }
    private sealed record CapturedRequest(string Url, string Body, string? RequestVerificationToken, string[] Options);
}
