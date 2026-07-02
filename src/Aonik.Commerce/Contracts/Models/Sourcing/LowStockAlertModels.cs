namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>One low-stock alert (Spec 052 §7/§10). Snapshots read meaningfully in the inbox even
/// after stock later moves; the live figure is always the level itself.</summary>
public record LowStockAlertDto(
    Guid Id,
    Guid IngredientId,
    string? IngredientName,
    decimal AvailableAtRaise,
    decimal ReorderPoint,
    string Status,
    DateTime RaisedAt);

/// <summary>Outcome of one low-stock scan pass (Spec 052 §9): newly opened vs. refreshed alerts.</summary>
public record LowStockScanResult(int Raised, int Refreshed);
