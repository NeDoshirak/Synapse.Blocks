using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Contracts.Cases;
using Synapse.Blocks.Api.Contracts.Students;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Data.Entities;
using Synapse.Blocks.Models;
using Synapse.Blocks.Services;

namespace Synapse.Blocks.Api.Students;

public sealed class ProgramEvaluationService(AppDbContext db, AttemptService attempts, BlockProgramRunner runner)
{
    public const int MaxRequestBytes = 256 * 1024;
    public const int MaxNodes = 500;
    public const int MaxConnections = 1000;

    public async Task<LevelEvaluationDto?> EvaluateAsync(
        TeacherCase sharedCase,
        string continuationToken,
        Guid attemptId,
        Guid levelVersionId,
        BlockProgram? program,
        CancellationToken cancellationToken = default)
    {
        if (program is null) throw new ProgramEvaluationValidationException(["A block program is required."]);
        if (JsonSerializer.SerializeToUtf8Bytes(program).Length > MaxRequestBytes
            || program.Nodes.Count > MaxNodes || program.Connections.Count > MaxConnections)
            throw new ProgramEvaluationSizeException();
        var validationErrors = runner.Validate(program);
        if (validationErrors.Count != 0) throw new ProgramEvaluationValidationException(validationErrors);

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [attemptId.ToString()], cancellationToken);
        var attempt = await attempts.FindAttemptAsync(sharedCase.Id, attemptId, continuationToken, cancellationToken);
        if (attempt is null) return null;

        var orderedVersionIds = JsonSerializer.Deserialize<List<Guid>>(attempt.LevelVersionIdsJson) ?? [];
        if (!orderedVersionIds.Contains(levelVersionId)) return null;
        var levelSnapshot = await db.LevelVersions.SingleOrDefaultAsync(item => item.Id == levelVersionId, cancellationToken);
        if (levelSnapshot is null) return null;
        var definition = JsonSerializer.Deserialize<LevelDefinition>(levelSnapshot.DefinitionJson, JsonOptions)
            ?? throw new InvalidOperationException("Level definition snapshot is invalid.");
        var orderedLevels = orderedVersionIds.ToArray();
        var completedIds = attempt.LevelResults.Where(result => result.Completed).Select(result => result.LevelVersionId).ToHashSet();
        var completedLevelCount = 0;
        while (completedLevelCount < orderedLevels.Length && completedIds.Contains(orderedLevels[completedLevelCount]))
            completedLevelCount++;
        AttemptStep? levelStep = null;
        if (attempt.Steps.Count > 0)
        {
            levelStep = attempt.Steps.SingleOrDefault(step => step.Kind == "level" && step.LevelVersionId == levelVersionId);
            if (levelStep is null) return null;
            if (!levelStep.Completed && attempt.ProgressionMode == Synapse.Blocks.Api.Contracts.Cases.CaseProgressionMode.Sequential)
            {
                var nextStep = attempt.Steps.OrderBy(step => step.Order).FirstOrDefault(step => !step.Completed);
                if (nextStep?.Id != levelStep.Id) throw new AttemptProgressConflictException();
            }
        }
        else
        {
            var levelIndex = orderedLevels.ToList().IndexOf(levelVersionId);
            if (levelIndex < completedLevelCount) { /* completed legacy levels remain selectable */ }
            else if (levelIndex > completedLevelCount) throw new AttemptProgressConflictException();
        }

        var runResults = runner.RunAll(program, definition);
        var passed = runResults.Count == definition.Tests.Count && runResults.All(result => result.Passed);
        var result = attempt.LevelResults.SingleOrDefault(item => item.LevelVersionId == levelVersionId);
        if (result is null)
        {
            result = new AttemptLevelResult { AttemptId = attemptId, LevelVersionId = levelVersionId, Completed = passed, CompletedAt = passed ? DateTimeOffset.UtcNow : null };
            db.AttemptLevelResults.Add(result);
        }
        else
        {
            result.Completed |= passed;
            result.CompletedAt ??= passed ? DateTimeOffset.UtcNow : null;
            result.LastSubmittedAt = DateTimeOffset.UtcNow;
        }

        if (passed && levelStep is not null)
        {
            levelStep.Completed = true;
            levelStep.CompletedAt ??= DateTimeOffset.UtcNow;
        }

        var isComplete = attempt.Steps.Count > 0
            ? attempt.Steps.All(step => step.Completed)
            : passed && completedLevelCount + 1 == orderedLevels.Length;
        if (isComplete)
        {
            attempt.Status = "Completed";
            attempt.CompletedAt ??= DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (passed) completedIds.Add(levelVersionId);

        var publicResults = runResults
            .Where(result => definition.Tests.Any(test => test.Id == result.TestId && !test.Hidden))
            .Select(result => new PublicTestResultDto(result.TestId, result.Name, result.Passed, result.Input, result.Expected, result.Actual, result.Error))
            .ToArray();
        return new LevelEvaluationDto(passed, publicResults, orderedVersionIds.Where(completedIds.Contains).ToArray(), attempt.Status == "Completed");
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class ProgramEvaluationSizeException() : Exception("Evaluation request exceeds configured limits.");
public sealed class ProgramEvaluationValidationException(IReadOnlyList<string> errors) : Exception("Program is invalid.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
public sealed class AttemptProgressConflictException() : Exception("Complete the next unlocked step in order.");
