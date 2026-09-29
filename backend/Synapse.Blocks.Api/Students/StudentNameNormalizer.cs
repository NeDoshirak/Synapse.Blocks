namespace Synapse.Blocks.Api.Students;

public static class StudentNameNormalizer
{
    public const int MaxDisplayNameLength = 80;

    public static bool TryNormalize(string? displayName, out string normalizedDisplayName, out string normalizedKey)
    {
        normalizedDisplayName = string.Join(' ', (displayName ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        normalizedKey = normalizedDisplayName.ToUpperInvariant();
        return normalizedDisplayName.Length is >= 1 and <= MaxDisplayNameLength;
    }
}
