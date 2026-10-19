using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Cases;
using Synapse.Blocks.Api.Contracts.Students;
using Synapse.Blocks.Api.Students;

namespace Synapse.Blocks.Api.Endpoints;

public static class StudentAttemptEndpoints
{
    private const string ContinuationCookie = "synapse.student.continuation.v2";

    public static IEndpointRouteBuilder MapStudentAttemptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/student/cases").AllowAnonymous();
        group.MapPost("/{token}/participants", async (
            string token,
            CreateParticipantRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            ShareLinkService links,
            AttemptService attempts,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var sharedCase = await links.ResolveCaseAsync(token, ct);
            if (sharedCase is null) return Results.NotFound(new { error = "Case unavailable." });
            var participant = await attempts.CreateOrContinueParticipantAsync(
                sharedCase.Id, request.DisplayName, context.Request.Cookies[ContinuationCookie], ct);
            if (participant is null) return Results.UnprocessableEntity(new { error = "Display name must contain 1 to 80 characters after trimming." });
            context.Response.Cookies.Append(ContinuationCookie, participant.Value.ContinuationToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = configuration.GetValue<bool?>("Cookie:Secure") ?? context.Request.IsHttps,
                Path = $"/api/student/cases/{token}",
                MaxAge = TimeSpan.FromDays(90)
            });
            return Results.Created($"/api/student/cases/{token}/participants/{participant.Value.Participant.ParticipantId}", participant.Value.Participant);
        }).WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        group.MapPost("/{token}/attempts", async (
            string token,
            HttpContext context,
            IAntiforgery antiforgery,
            ShareLinkService links,
            AttemptService attempts,
            CancellationToken ct) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var sharedCase = await links.ResolveCaseAsync(token, ct);
            if (sharedCase is null) return Results.NotFound(new { error = "Case unavailable." });
            var attempt = await attempts.StartAttemptAsync(sharedCase.Id, token, context.Request.Cookies[ContinuationCookie], ct);
            return attempt is null ? Results.NotFound() : Results.Created($"/api/student/cases/{token}/attempts/{attempt.AttemptId}", attempt);
        });

        group.MapGet("/{token}/attempts/current", async (
            string token, HttpContext context, ShareLinkService links, AttemptService attempts, CancellationToken ct) =>
        {
            var sharedCase = await links.ResolveCaseAsync(token, ct);
            if (sharedCase is null) return Results.NotFound(new { error = "Case unavailable." });
            var attempt = await attempts.GetCurrentAsync(sharedCase.Id, token, context.Request.Cookies[ContinuationCookie], ct);
            return attempt is null ? Results.NotFound() : Results.Ok(attempt);
        });

        group.MapPost("/{token}/attempts/{attemptId:guid}/steps/{stepId:guid}/complete", async (
            string token,
            Guid attemptId,
            Guid stepId,
            HttpContext context,
            IAntiforgery antiforgery,
            ShareLinkService links,
            AttemptService attempts,
            CancellationToken ct) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var sharedCase = await links.ResolveCaseAsync(token, ct);
            if (sharedCase is null) return Results.NotFound(new { error = "Case unavailable." });
            try
            {
                var attempt = await attempts.CompleteMarkdownStepAsync(sharedCase.Id, attemptId, stepId, token,
                    context.Request.Cookies[ContinuationCookie], ct);
                return attempt is null ? Results.NotFound() : Results.Ok(attempt);
            }
            catch (AttemptProgressConflictException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        group.MapGet("/{token}/attempts/{attemptId:guid}/assets/{assetId:guid}/content", async (
            string token,
            Guid attemptId,
            Guid assetId,
            ShareLinkService links,
            HttpContext context,
            CancellationToken ct) =>
        {
            var asset = await links.GetAttemptMarkdownAssetAsync(token, attemptId, assetId, ct);
            if (asset is null) return Results.NotFound();
            context.Response.Headers["Cache-Control"] = "private, max-age=86400, immutable";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(asset.Content, asset.ContentType);
        });

        group.MapPost("/{token}/attempts/{attemptId:guid}/levels/{levelVersionId:guid}/evaluate", async (
            string token,
            Guid attemptId,
            Guid levelVersionId,
            Synapse.Blocks.Models.BlockProgram program,
            HttpContext context,
            IAntiforgery antiforgery,
            ShareLinkService links,
            ProgramEvaluationService evaluator,
            CancellationToken ct) =>
        {
            if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context)) return Results.BadRequest();
            var sharedCase = await links.ResolveCaseAsync(token, ct);
            if (sharedCase is null) return Results.NotFound(new { error = "Case unavailable." });
            try
            {
                var evaluation = await evaluator.EvaluateAsync(sharedCase, context.Request.Cookies[ContinuationCookie] ?? "", attemptId, levelVersionId, program, ct);
                return evaluation is null ? Results.NotFound() : Results.Ok(evaluation);
            }
            catch (ProgramEvaluationSizeException)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            catch (ProgramEvaluationValidationException ex)
            {
                return Results.UnprocessableEntity(new { errors = ex.Errors });
            }
            catch (AttemptProgressConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).WithMetadata(new RequestSizeLimitAttribute(ProgramEvaluationService.MaxRequestBytes));
        return app;
    }
}
