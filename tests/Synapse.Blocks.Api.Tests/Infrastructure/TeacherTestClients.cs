using System.Net;
using System.Net.Http.Json;

namespace Synapse.Blocks.Api.Tests.Infrastructure;

public static class TeacherTestClients
{
    public const string AdminEmail = "admin@example.test";
    public const string AdminPassword = "Admin-password-123!";
    public const string TeacherPassword = "Teacher-password-123!";

    public static async Task<HttpClient> CreateAsync(ApiWebApplicationFactory factory)
    {
        using var admin = factory.CreateClient(new() { HandleCookies = true });
        var csrf = await admin.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        admin.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var login = await admin.PostAsJsonAsync("/api/auth/sign-in", new { email = AdminEmail, password = AdminPassword });
        if (login.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Test platform admin sign-in failed.");
        csrf = await admin.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        admin.DefaultRequestHeaders.Remove("RequestVerificationToken");
        admin.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);

        var email = $"teacher-{Guid.NewGuid():N}@example.test";
        var issued = await admin.PostAsJsonAsync("/api/platform/invitations", new { email });
        if (issued.StatusCode != HttpStatusCode.Created) throw new InvalidOperationException("Test teacher invitation could not be created.");
        var invitation = await issued.Content.ReadFromJsonAsync<InvitationDto>();
        csrf = await admin.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        admin.DefaultRequestHeaders.Remove("RequestVerificationToken");
        admin.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var accepted = await admin.PostAsJsonAsync("/api/platform/invitations/accept", new { token = invitation!.Token, password = TeacherPassword });
        if (accepted.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Test teacher invitation could not be accepted.");

        var teacher = factory.CreateClient(new() { HandleCookies = true });
        csrf = await teacher.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        teacher.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        var teacherLogin = await teacher.PostAsJsonAsync("/api/auth/sign-in", new { email, password = TeacherPassword });
        if (teacherLogin.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Test teacher sign-in failed.");
        csrf = await teacher.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        teacher.DefaultRequestHeaders.Remove("RequestVerificationToken");
        teacher.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        return teacher;
    }

    private sealed record CsrfDto(string RequestToken);
    private sealed record InvitationDto(string Token);
}
