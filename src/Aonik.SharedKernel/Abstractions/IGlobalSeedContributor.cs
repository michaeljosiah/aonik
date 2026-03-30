namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for global (non-tenant-scoped) data seeding.
/// Each module registers implementations for its startup seed routines,
/// allowing them to be triggered on demand via admin endpoints.
///
/// Resolved as <c>IEnumerable&lt;IGlobalSeedContributor&gt;</c>
/// by the data seed endpoint.
/// </summary>
public interface IGlobalSeedContributor
{
    /// <summary>
    /// Unique key identifying this seed (e.g. "Identity", "Catalog", "PromptSpecs").
    /// Used by the admin UI to select which seeds to run.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Human-readable display name (e.g. "Global Permissions", "Prompt Templates").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Short description of what this seed does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Sort order for display in the admin UI.
    /// </summary>
    int SortOrder { get; }

    /// <summary>
    /// Runs the seed. Returns a list of operation descriptions for audit/display.
    /// Implementations must be idempotent.
    /// </summary>
    Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default);
}
