using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;
using Aonik.SharedKernel.Abstractions.Packs;

namespace Aonik.Cli.Commands;

/// <summary>
/// Offline inspection of business-type configuration packs (Spec 065). Reads the manifests embedded
/// in Aonik.SharedKernel — no API, session, or database — so it is a self-contained way to verify the
/// packs load and to see exactly what a tenant of a given business type would be configured with.
/// </summary>
public sealed class PacksCommandHandler
{
    private readonly IConfigPackSource _packSource;
    private readonly ICliOutputWriter _outputWriter;

    public PacksCommandHandler(IConfigPackSource packSource, ICliOutputWriter outputWriter)
    {
        _packSource = packSource;
        _outputWriter = outputWriter;
    }

    public async Task<int> ListAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var summaries = _packSource.ListBusinessTypes()
            .Select(_packSource.Get)
            .Where(manifest => manifest is not null)
            .Select(manifest => new PackSummary(
                manifest!.BusinessType,
                manifest.Version,
                manifest.DisplayName,
                manifest.Modules.Count,
                manifest.Settings.Count,
                manifest.Agents.Count,
                manifest.ReferenceData.Sum(group => group.Items.Count)))
            .ToList();

        await _outputWriter.WriteCollectionAsync(summaries, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ShowAsync(string businessType, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var manifest = _packSource.Get(businessType);
        if (manifest is null)
        {
            throw new AonikCliException($"No config pack found for business type '{businessType}'.");
        }

        await _outputWriter.WriteObjectAsync(manifest, outputMode, cancellationToken);
        return 0;
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
