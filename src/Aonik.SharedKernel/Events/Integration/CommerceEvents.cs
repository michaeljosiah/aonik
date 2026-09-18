namespace Aonik.SharedKernel.Events.Integration;

// ── Commerce-originated integration events ───────────────────────────────────
// Published by the Commerce module. Platform subscribes to surface operational
// signals through the admin notification inbox (Spec 016).

/// <summary>
/// Raised when the low-stock scan opens a NEW low-stock alert for an ingredient (Spec 052 §9) —
/// refreshes of an existing active alert do not re-raise. Platform subscribes to notify the
/// tenant's admins through the Spec 016 realtime inbox ("Rice: 2 kg available, reorder at 5 kg").
/// </summary>
public record LowStockAlertRaisedEvent(
    Guid TenantId,
    Guid AlertId,
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal Available,
    decimal ReorderPoint) : ITenantScopedEvent;
