using System.Collections.Concurrent;
using System.Text.Json;
using Aonik.Platform.Contracts.Models.Packs;
using Aonik.Platform.Contracts.Services.Packs;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Packs;

/// <summary>
/// Loads config-pack manifests from embedded JSON resources under
/// <c>Services/Packs/Data/{businessType}.pack.json</c> (Spec 065). Manifests are immutable, so
/// results are cached. An unknown business type (no resource) resolves to <c>null</c> — a no-op pack.
/// </summary>
internal sealed class ConfigPackSource : IConfigPackSource
{
    private const string ResourcePrefix = "Aonik.Platform.Services.Packs.Data.";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Cached because embedded manifests never change at runtime; null is a valid cached value (no pack).
    private static readonly ConcurrentDictionary<string, ConfigPackManifest?> Cache = new();

    public ConfigPackManifest? Get(string businessType)
        => Cache.GetOrAdd(BusinessTypes.Normalize(businessType), Load);

    private static ConfigPackManifest? Load(string businessType)
    {
        var assembly = typeof(ConfigPackSource).Assembly;
        using var stream = assembly.GetManifestResourceStream($"{ResourcePrefix}{businessType}.pack.json");
        if (stream is null)
        {
            return null; // no manifest for this type → no-op
        }

        var manifest = JsonSerializer.Deserialize<ConfigPackManifest>(stream, Options)
            ?? throw new InvalidOperationException($"Config pack '{businessType}.pack.json' failed to deserialize.");

        return manifest;
    }
}
