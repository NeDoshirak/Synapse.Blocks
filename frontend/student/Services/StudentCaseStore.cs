using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Services;

public sealed class StudentCaseStore(HttpClient http)
{
    private string? _requestToken;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<StudentCaseDto> LoadAsync(string token, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        using var response = await SendAsync(HttpMethod.Get, $"api/student/cases/{Uri.EscapeDataString(token)}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Набор недоступен.", null, response.StatusCode);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<StudentCaseDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<StudentAttemptDto> StartAsync(string token, string? displayName, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        var enteredName = displayName ?? "";
        var normalized = string.Join(' ', enteredName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is < 1 or > 80) throw new ArgumentException("Имя должно содержать от 1 до 80 символов.", nameof(displayName));
        var requestToken = await GetRequestTokenAsync(cancellationToken);
        using var participant = await SendAsync(HttpMethod.Post, $"api/student/cases/{Uri.EscapeDataString(token)}/participants", new { displayName = enteredName.Trim() }, cancellationToken, requestToken);
        if (participant.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Набор недоступен.", null, participant.StatusCode);
        await EnsureSuccessAsync(participant);
        using var attempt = await SendAsync(HttpMethod.Post, $"api/student/cases/{Uri.EscapeDataString(token)}/attempts", new { }, cancellationToken, requestToken);
        if (attempt.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Не удалось начать попытку. Повторите вход по QR-коду.", null, attempt.StatusCode);
        await EnsureSuccessAsync(attempt);
        return (await attempt.Content.ReadFromJsonAsync<StudentAttemptDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<StudentAttemptDto?> ResumeAsync(string token, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        using var response = await SendAsync(HttpMethod.Get, $"api/student/cases/{Uri.EscapeDataString(token)}/attempts/current", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        var attempt = await response.Content.ReadFromJsonAsync<StudentAttemptDto>(JsonOptions, cancellationToken);
        return attempt?.Status == "InProgress" ? attempt : null;
    }

    private async Task<string> GetRequestTokenAsync(CancellationToken cancellationToken)
    {
        if (_requestToken is not null) return _requestToken;
        using var response = await SendAsync(HttpMethod.Get, "api/auth/csrf", null, cancellationToken);
        await EnsureSuccessAsync(response);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return _requestToken = json.RootElement.GetProperty("requestToken").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken, string? requestToken = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        if (requestToken is not null) request.Headers.TryAddWithoutValidation("RequestVerificationToken", requestToken);
        return await http.SendAsync(request, cancellationToken);
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        return Task.FromException(new HttpRequestException("Не удалось связаться с сервером. Проверьте соединение и повторите попытку.", null, response.StatusCode));
    }

    private static void ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128 || token.Any(char.IsWhiteSpace)) throw new ArgumentException("Некорректная ссылка на набор.", nameof(token));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}
