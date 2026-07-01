using Aonik.Commerce.Contracts.Models.Sourcing;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Low-stock alerting over ingredient levels (Spec 052 §9/§10). The scan raises at most one ACTIVE
/// (Open/Acknowledged) alert per (tenant, ingredient) and refreshes it thereafter — it never
/// re-opens an acknowledged alert and never auto-resolves on incidental restock. Ordered/Resolved
/// transitions belong to the procurement specs (053/054).
/// </summary>
public interface ILowStockAlertService
{
    /// <summary>
    /// Cross-tenant scan (Quartz-driven, system context): for every ingredient level with a reorder
    /// point whose available (OnHand - Reserved) is at/below it, raises a new Open alert — enqueueing
    /// the <c>LowStockAlertRaisedEvent</c> for the Spec 016 inbox on NEW alerts only — or silently
    /// refreshes the existing active alert's snapshot. Idempotent per Spec 052 §9.
    /// </summary>
    Task<LowStockScanResult> ScanAndRaiseAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the tenant's alerts, newest first, optionally filtered to one status.</summary>
    Task<IReadOnlyList<LowStockAlertDto>> ListAsync(string? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an Open alert Acknowledged (an operator is handling it; still active, so the scan keeps
    /// refreshing it rather than raising a second). Idempotent when already Acknowledged; throws when
    /// the alert has left the active set; null when not found.
    /// </summary>
    Task<LowStockAlertDto?> AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default);
}
