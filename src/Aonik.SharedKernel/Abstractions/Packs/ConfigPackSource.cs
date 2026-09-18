using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Aonik.SharedKernel.Modules;

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

        var manifest = JsonSerializer.Deserialize<ConfigPackManifest>(stream, Options)
            ?? throw new InvalidOperationException($"Config pack '{businessType}{ResourceSuffix}' failed to deserialize.");

        Validate(manifest, $"{businessType}{ResourceSuffix}");
        return manifest;
    }

    /// <summary>
    /// Validates a manifest against the module catalogue (Spec 097 §13): every entry of
    /// <see cref="ConfigPackManifest.Modules"/> must be a canonical <see cref="ModuleIds"/> id, compared
    /// case-sensitively. A pack that names an unknown module fails to load — loudly, at first use — rather
    /// than silently provisioning a tenant with the wrong module set. Public so tooling and tests can
    /// validate a manifest that did not come from the embedded resources.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <param name="packName">A label for the pack used in the error message (e.g. the resource file name).</param>
    /// <exception cref="InvalidOperationException">A module id is blank or not in the catalogue.</exception>
    public static void Validate(ConfigPackManifest manifest, string packName)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        foreach (var moduleId in manifest.Modules)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                throw new InvalidOperationException(
                    $"Config pack '{packName}' declares a blank module id; every entry of 'modules' must be a catalogue id.");
            }

            if (!ModuleCatalog.IsKnown(moduleId))
            {
                var known = string.Join(", ", ModuleCatalog.All.Select(descriptor => descriptor.Id));
                throw new InvalidOperationException(
                    $"Config pack '{packName}' declares module '{moduleId}', which is not a module in the catalogue. " +
                    $"Module ids are case-sensitive canonical ids; known ids: {known}.");
            }
        }
    }
}
