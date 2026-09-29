using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Synapse.Blocks.Models;
using Synapse.Blocks.Serialization;
using System.Net.Http.Json;

namespace Synapse.Blocks.Services;

public sealed class StudentCaseStore(HttpClient http)
{
    private string? _requestToken;
    public async Task<StudentCaseDto> LoadAsync(string token, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        using var response = await SendAsync(HttpMethod.Get, $"api/student/cases/{Uri.EscapeDataString(token)}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Набор недоступен.", null, response.StatusCode);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.StudentCaseDto, cancellationToken))!;
    }

    public async Task<StudentAttemptDto> StartAsync(string token, string? displayName, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        var enteredName = displayName ?? "";
        var normalized = string.Join(' ', enteredName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is < 1 or > 80) throw new ArgumentException("Имя должно содержать от 1 до 80 символов.", nameof(displayName));
        var requestToken = await GetRequestTokenAsync(cancellationToken);
        using var participant = await SendAsync(HttpMethod.Post, $"api/student/cases/{Uri.EscapeDataString(token)}/participants", new CreateStudentParticipantRequest(enteredName.Trim()), cancellationToken, requestToken);
        if (participant.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Набор недоступен.", null, participant.StatusCode);
        await EnsureSuccessAsync(participant);
        using var attempt = await SendAsync(HttpMethod.Post, $"api/student/cases/{Uri.EscapeDataString(token)}/attempts", new EmptyStudentRequest(), cancellationToken, requestToken);
        if (attempt.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Не удалось начать попытку. Повторите вход по QR-коду.", null, attempt.StatusCode);
        await EnsureSuccessAsync(attempt);
        return (await attempt.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.StudentAttemptDto, cancellationToken))!;
    }

    public async Task<StudentAttemptDto?> ResumeAsync(string token, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        using var response = await SendAsync(HttpMethod.Get, $"api/student/cases/{Uri.EscapeDataString(token)}/attempts/current", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        var attempt = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.StudentAttemptDto, cancellationToken);
        return attempt?.Status == "InProgress" ? attempt : null;
    }

    public async Task<LevelEvaluationDto> EvaluateAsync(string token, Guid attemptId, Guid levelVersionId, BlockProgram program, CancellationToken cancellationToken = default)
    {
        ValidateToken(token);
        var requestToken = await GetRequestTokenAsync(cancellationToken);
        using var response = await SendAsync(HttpMethod.Post, $"api/student/cases/{Uri.EscapeDataString(token)}/attempts/{attemptId}/levels/{levelVersionId}/evaluate", program, cancellationToken, requestToken);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new HttpRequestException("Попытка больше недоступна.", null, response.StatusCode);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.LevelEvaluationDto, cancellationToken))!;
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
        if (body is not null)
        {
            request.Content = body switch
            {
                CreateStudentParticipantRequest participant => JsonContent.Create(participant, AppJsonSerializerContext.Default.CreateStudentParticipantRequest),
                EmptyStudentRequest empty => JsonContent.Create(empty, AppJsonSerializerContext.Default.EmptyStudentRequest),
                BlockProgram program => JsonContent.Create(program, AppJsonSerializerContext.Default.BlockProgram),
                _ => throw new InvalidOperationException("Unsupported student API request payload.")
            };
        }
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

}
