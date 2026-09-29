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

        var levelIndex = sharedCase.Levels.OrderBy(level => level.Order).ToList().FindIndex(level => level.LevelVersionId == levelVersionId);
        if (levelIndex < 0) return null;
        var levelSnapshot = sharedCase.Levels.Single(level => level.LevelVersionId == levelVersionId).LevelVersion;
        var definition = JsonSerializer.Deserialize<LevelDefinition>(levelSnapshot.DefinitionJson, JsonOptions)
            ?? throw new InvalidOperationException("Level definition snapshot is invalid.");

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [attemptId.ToString()], cancellationToken);
        var attempt = await attempts.FindAttemptAsync(sharedCase.Id, attemptId, continuationToken, cancellationToken);
        if (attempt is null) return null;

        var orderedLevels = sharedCase.Levels.OrderBy(level => level.Order).ToArray();
        var completedIds = attempt.LevelResults.Where(result => result.Completed).Select(result => result.LevelVersionId).ToHashSet();
        var completedLevelCount = 0;
        while (completedLevelCount < orderedLevels.Length && completedIds.Contains(orderedLevels[completedLevelCount].LevelVersionId))
            completedLevelCount++;
        if (levelIndex > completedLevelCount) throw new AttemptProgressConflictException();

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

        if (passed && completedLevelCount + 1 == orderedLevels.Length)
        {
            attempt.Status = "Completed";
            attempt.CompletedAt ??= DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var publicResults = runResults
            .Where(result => definition.Tests.Any(test => test.Id == result.TestId && !test.Hidden))
            .Select(result => new PublicTestResultDto(result.TestId, result.Name, result.Passed, result.Input, result.Expected, result.Actual, result.Error))
            .ToArray();
        return new LevelEvaluationDto(passed, publicResults);
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
public sealed class AttemptProgressConflictException() : Exception("Complete the next level in order.");
