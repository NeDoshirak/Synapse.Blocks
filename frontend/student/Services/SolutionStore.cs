using System.Text.Json;
using Microsoft.JSInterop;
using Synapse.Blocks.Models;
using Synapse.Blocks.Serialization;

namespace Synapse.Blocks.Services;

/// <summary>Хранит черновик графа отдельно для каждого уровня на этом устройстве.</summary>
public sealed class SolutionStore(IJSRuntime js)
{
    private const string StoragePrefix = "synapse-solution-v1-";
    private const string CaseStoragePrefix = "synapse-case-solution-v1-";
    private static string Key(Guid levelId) => $"{StoragePrefix}{levelId:N}";
    private static string CaseKey(Guid attemptId, Guid levelVersionId) => $"{CaseStoragePrefix}{attemptId:N}-{levelVersionId:N}";

    public async Task<BlockProgram?> LoadAsync(Guid levelId)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", Key(levelId));
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.BlockProgram);
        }
        catch { return null; }
    }

    public async Task<BlockProgram?> LoadCaseAsync(Guid attemptId, Guid levelVersionId)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", CaseKey(attemptId, levelVersionId));
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.BlockProgram);
        }
        catch { return null; }
    }

    public async Task SaveCaseAsync(Guid attemptId, Guid levelVersionId, BlockProgram program)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", CaseKey(attemptId, levelVersionId), JsonSerializer.Serialize(program, AppJsonSerializerContext.Default.BlockProgram)); }
        catch (JSException) { }
    }

    public async Task SaveAsync(Guid levelId, BlockProgram program)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key(levelId), JsonSerializer.Serialize(program, AppJsonSerializerContext.Default.BlockProgram));
        }
        catch (JSException) { }
    }

    public async Task RemoveAsync(Guid levelId) => await js.InvokeVoidAsync("localStorage.removeItem", Key(levelId));

    /// <summary>Удаляет черновики всех уровней перед передачей компьютера следующему игроку.</summary>
    public async Task ResetAllAsync()
    {
        await js.InvokeVoidAsync("synapseStorage.removeByPrefix", StoragePrefix);
        await js.InvokeVoidAsync("synapseStorage.removeByPrefix", CaseStoragePrefix);
    }
}
