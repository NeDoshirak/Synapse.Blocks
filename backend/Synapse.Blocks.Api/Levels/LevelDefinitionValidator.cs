using System.Text.Json;
using Synapse.Blocks.Models;

namespace Synapse.Blocks.Api.Levels;

public sealed class LevelDefinitionValidator
{
    public const int MaxSerializedBytes = 5 * 1024 * 1024;

    public IReadOnlyDictionary<string, string[]> Validate(LevelDefinition? definition)
    {
        var errors = new Dictionary<string, string[]>();
        if (definition is null)
            return new Dictionary<string, string[]> { ["definition"] = ["A level definition is required."] };
        if (string.IsNullOrWhiteSpace(definition.Title) || definition.Title.Trim().Length > 200)
            errors["title"] = ["Title must contain between 1 and 200 characters."];
        if ((definition.Tests?.Count ?? 0) > 500)
            errors["tests"] = ["A level can contain at most 500 tests."];
        if ((definition.IntroSteps?.Count ?? 0) > 100)
            errors["introSteps"] = ["A level can contain at most 100 introduction steps."];
        if (definition.AllowedBlocks is null || definition.AllowedBlocks.Count == 0)
            errors["allowedBlocks"] = ["At least one block type must be allowed."];

        try
        {
            if (JsonSerializer.SerializeToUtf8Bytes(definition).Length > MaxSerializedBytes)
                errors["definition"] = ["The serialized level definition cannot exceed 5 MiB."];
        }
        catch (JsonException)
        {
            errors["definition"] = ["The level definition could not be serialized."];
        }
        return errors;
    }
}
