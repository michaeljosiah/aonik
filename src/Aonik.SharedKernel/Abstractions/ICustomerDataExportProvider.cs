using System.Text.Json;

namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for exporting customer-scoped data owned by a module.
/// Each module implements this to contribute its portion of the customer data graph
/// to the export bundle.
/// </summary>
public interface ICustomerDataExportProvider
{
    /// <summary>
    /// Exports all entities owned by this module for the given user(s) in the tenant.
    /// Returns a dictionary keyed by entity type name → list of serialised entities.
    /// </summary>
    Task<Dictionary<string, List<JsonElement>>> ExportAsync(
        Guid tenantId,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cross-module contract for importing customer-scoped data owned by a module.
/// Each module implements this to consume its portion of the customer data graph
/// from the import bundle.
/// </summary>
public interface ICustomerDataImportConsumer
{
    /// <summary>
    /// Imports entities owned by this module from the bundle data.
    /// The <paramref name="idMap"/> contains old-to-new GUID mappings for FK remapping.
    /// The consumer must remap all FK references and set TenantId on all entities.
    /// </summary>
    Task<CustomerDataImportModuleResult> ImportAsync(
        Guid tenantId,
        Dictionary<string, List<JsonElement>> data,
        Dictionary<Guid, Guid> idMap,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from a single module's import operation.
/// </summary>
public record CustomerDataImportModuleResult(
    Dictionary<string, int> EntityCounts,
    List<string> Warnings);

/// <summary>
/// The full export bundle for a customer's data graph.
/// Serialised as a flat JSON file with entity-type keys.
/// </summary>
public record CustomerDataBundle
{
    public required string Version { get; init; }
    public required DateTime ExportedAt { get; init; }
    public string? SourceEnvironment { get; init; }
    public required Guid SourceTenantId { get; init; }
    public required Guid RootPartyId { get; init; }
    public Dictionary<string, int> EntityCounts { get; init; } = new();
    public Dictionary<string, List<JsonElement>> Data { get; init; } = new();
}

/// <summary>
/// Result returned after importing a customer data bundle.
/// </summary>
public record CustomerDataImportResult
{
    public required Guid NewPartyId { get; init; }
    public Dictionary<string, int> EntityCounts { get; init; } = new();
    public Dictionary<Guid, Guid> IdMap { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
