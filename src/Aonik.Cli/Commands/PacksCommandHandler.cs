using System.Reflection;
using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Offline inspection of business-type configuration packs (Spec 065). Reads the config-pack manifests
/// embedded into this CLI (linked from Aonik.SharedKernel at build time) — no API, session, database, or
/// SharedKernel/ASP.NET reference. A self-contained way to verify the packs load and see exactly what a
/// tenant of a given business type would be configured with. Manifests are handled as raw JSON so the
/// CLI needs no dependency on the typed pack model.
/// </summary>
public sealed class PacksCommandHandler
{
    private const string ResourcePrefix = "Aonik.Cli.Packs.";
    private const string ResourceSuffix = ".pack.json";
    private static readonly Assembly Assembly = typeof(PacksCommandHandler).Assembly;

    private readonly ICliOutputWriter _outputWriter;

    public PacksCommandHandler(ICliOutputWriter outputWriter)
    {
        _outputWriter = outputWriter;
    }

    public async Task<int> ListAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var summaries = Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(name => name.Substring(ResourcePrefix.Length, name.Length - ResourcePrefix.Length - ResourceSuffix.Length))
            .OrderBy(businessType => businessType, StringComparer.Ordinal)
            .Select(Summarize)
            .ToList();

        await _outputWriter.WriteCollectionAsync(summaries, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ShowAsync(string businessType, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var key = Normalize(businessType);
        var json = ReadManifest(key);
        if (json is null)
        {
            throw new AonikCliException($"No config pack found for business type '{businessType}'.");
        }

        using var document = JsonDocument.Parse(json);
        await _outputWriter.WriteObjectAsync(document.RootElement.Clone(), outputMode, cancellationToken);
        return 0;
    }

    private static string Normalize(string? businessType)
        => string.IsNullOrWhiteSpace(businessType) ? "base" : businessType.Trim().ToLowerInvariant();

    private static string? ReadManifest(string businessType)
    {
        using var stream = Assembly.GetManifestResourceStream($"{ResourcePrefix}{businessType}{ResourceSuffix}");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static PackSummary Summarize(string businessType)
    {
        using var document = JsonDocument.Parse(ReadManifest(businessType)!);
        var root = document.RootElement;

        int ArrayLength(string property)
            => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Array
                ? element.GetArrayLength() : 0;

        int ObjectLength(string property)
            => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Object
                ? element.EnumerateObject().Count() : 0;

        var referenceDataItems = root.TryGetProperty("referenceData", out var groups) && groups.ValueKind == JsonValueKind.Array
            ? groups.EnumerateArray().Sum(group =>
                group.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array ? items.GetArrayLength() : 0)
            : 0;

        return new PackSummary(
            root.TryGetProperty("businessType", out var bt) ? bt.GetString() ?? businessType : businessType,
            root.TryGetProperty("version", out var v) && v.TryGetInt32(out var version) ? version : 0,
            root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
            ArrayLength("modules"),
            ObjectLength("settings"),
            ArrayLength("agents"),
            referenceDataItems);
    }
}

/// <summary>A one-line summary of a config pack for `packs list`.</summary>
public sealed record PackSummary(
    string BusinessType,
    int Version,
    string? DisplayName,
    int Modules,
    int Settings,
    int Agents,
    int ReferenceDataItems);
