using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Reports;

namespace Synapse.Blocks.Api.Endpoints;

public static class TeacherReportEndpoints
{
    public static IEndpointRouteBuilder MapTeacherReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/teacher/cases/{caseId:guid}/report", async (
            Guid caseId, ICurrentUser current, CaseReportService reports, CancellationToken ct) =>
        {
            var report = await reports.GetAsync(current.UserId, caseId, ct);
            return report is null ? Results.NotFound() : Results.Ok(report);
        }).RequireAuthorization();
        return app;
    }
}
