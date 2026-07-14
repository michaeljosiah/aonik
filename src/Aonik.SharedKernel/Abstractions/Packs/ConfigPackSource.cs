using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Aonik.SharedKernel.Abstractions.Packs;

/// <summary>
/// Loads config-pack manifests from embedded JSON resources under
/// <c>Abstractions/Packs/Data/{businessType}.pack.json</c> (Spec 065). Manifests are immutable, so
/// results are cached. An unknown business type (no resource) resolves to <c>null</c> — a no-op pack.
/// Public + parameterless so tooling (e.g. the CLI) can read manifests offline with no DI.
/// </summary>
public sealed class ConfigPackSource : IConfigPackSource
{
    private const string ResourcePrefix = "Aonik.SharedKernel.Abstractions.Packs.Data.";
    private const string ResourceSuffix = ".pack.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Cached because embedded manifests never change at runtime; null is a valid cached value (no pack).
    private static readonly ConcurrentDictionary<string, ConfigPackManifest?> Cache = new();

    private static Assembly Assembly => typeof(ConfigPackSource).Assembly;

    public ConfigPackManifest? Get(string businessType)
        => Cache.GetOrAdd(BusinessTypes.Normalize(businessType), Load);

    public IReadOnlyList<string> ListBusinessTypes()
        => Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(name => name.Substring(ResourcePrefix.Length, name.Length - ResourcePrefix.Length - ResourceSuffix.Length))
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToList();

    private static ConfigPackManifest? Load(string businessType)
    {
        using var stream = Assembly.GetManifestResourceStream($"{ResourcePrefix}{businessType}{ResourceSuffix}");
        if (stream is null)
        {
            return null; // no manifest for this type → no-op
        }

        return JsonSerializer.Deserialize<ConfigPackManifest>(stream, Options)
            ?? throw new InvalidOperationException($"Config pack '{businessType}{ResourceSuffix}' failed to deserialize.");
    }
}
