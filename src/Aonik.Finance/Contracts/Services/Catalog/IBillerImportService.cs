using Aonik.Finance.Contracts.Models.Catalog;

namespace Aonik.Finance.Contracts.Services.Catalog;

/// <summary>
/// Imports a partner connector's biller catalogue into the AONIK catalogue (Spec 040). Both operations
/// are Catalog.Write-gated and tenant-scoped (tenant resolved from <c>ITenantContext</c>, never the
/// request). No money moves — this is a reference-data sync.
/// </summary>
public interface IBillerImportService
{
    /// <summary>
    /// Lists the current tenant's configured connectors that can supply a biller catalogue (the
    /// wizard's Source step) — i.e. connectors whose type has a registered bill-payment implementation.
    /// </summary>
    Task<BillerImportSourcesResponse> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live catalogue from the connector and annotates each biller as New / Mapped / Changed
    /// against what is already imported for that connector. Persists nothing.
    /// </summary>
    Task<BillerImportPreviewResponse> PreviewAsync(
        BillerImportPreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotent upsert of the selected billers, their services, and the connector mappings: creates
    /// new rows, refreshes changed names/amounts, and soft-deactivates services the partner no longer
    /// offers. Identity is the connector mapping, so re-running never duplicates.
    /// </summary>
    Task<BillerImportSummaryResponse> ImportAsync(
        BillerImportRequest request, CancellationToken cancellationToken = default);
}
