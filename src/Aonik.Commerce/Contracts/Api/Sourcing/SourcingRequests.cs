namespace Aonik.Commerce.Contracts.Api.Sourcing;

/// <summary>HTTP request bodies for the ingredient admin endpoints (Spec 050). Mapped to service commands.</summary>
public record CreateIngredientRequest(
    string Name,
    string BaseUnit,
    string? Sku,
    string? Category,
    string? Notes);

public record UpdateIngredientRequest(
    string Name,
    string BaseUnit,
    string? Sku,
    string? Category,
    string? Notes,
    bool? IsActive = null);

/// <summary>Sets a new effective-dated unit cost for an ingredient (Spec 051 §8). Omit
/// <paramref name="EffectiveFrom"/> for "now"; a future date schedules the cost (R4).</summary>
public record SetIngredientCostRequest(
    string Currency,
    decimal UnitCost,
    DateTime? EffectiveFrom = null);

/// <summary>Registers a supplier (Spec 053 §9). Name unique per tenant; Currency is the ISO 4217
/// code we buy in; PartyId optionally soft-links a platform counterparty Party.</summary>
public record CreateSupplierRequest(
    string Name,
    string Currency,
    Guid? PartyId = null,
    int? LeadTimeDays = null,
    string? PaymentTerms = null);

/// <summary>Updates a supplier (Spec 053 §9). Omit <paramref name="IsActive"/> to leave the
/// stored active state unchanged.</summary>
public record UpdateSupplierRequest(
    string Name,
    string Currency,
    Guid? PartyId = null,
    int? LeadTimeDays = null,
    string? PaymentTerms = null,
    bool? IsActive = null);

/// <summary>Upserts one supplier price-list row (Spec 053 §9) — keyed by (supplier, ingredient).
/// PackSize is in the ingredient's base unit; omit Currency to default to the supplier's.</summary>
public record UpsertSupplierIngredientRequest(
    Guid IngredientId,
    decimal PackSize,
    decimal PackPrice,
    string? Currency = null,
    string? Sku = null,
    int? LeadTimeDays = null);

/// <summary>One purchase-order line (Spec 053 §10): quantity in the ingredient's base unit; omit
/// UnitPrice to default from the supplier catalog (PackPrice / PackSize).</summary>
public record PurchaseOrderLineRequest(
    Guid IngredientId,
    decimal Quantity,
    decimal? UnitPrice = null);

/// <summary>Creates a purchase order from explicit lines (Spec 053 §10) — an Order with
/// OrderType "PurchaseOrder" on the shared spine. Omit Currency for the supplier's currency.</summary>
public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    List<PurchaseOrderLineRequest> Lines,
    string? Currency = null,
    string? Notes = null,
    string? IdempotencyKey = null);

/// <summary>Seeds a purchase order from low-stock alerts (Spec 053 §12). Omit AlertIds for auto:
/// every Open/Acknowledged alert for an ingredient this supplier has a catalog row for.</summary>
public record CreatePurchaseOrderFromShortfallRequest(
    Guid SupplierId,
    List<Guid>? AlertIds = null,
    string? Notes = null,
    string? IdempotencyKey = null);

/// <summary>Cancels a purchase order before receipt (Spec 053 §13).</summary>
public record CancelPurchaseOrderRequest(string? Reason = null);

/// <summary>One received line (Spec 054 §7): quantity in the ingredient's base unit; a non-null
/// UnitCostActual (per base unit, in the purchase order's currency) also refreshes the
/// ingredient's Spec 051 cost effective from the receipt date.</summary>
public record ReceiveGoodsLineRequest(
    Guid IngredientId,
    decimal QuantityReceived,
    decimal? UnitCostActual = null);

/// <summary>Receives goods against a submitted purchase order (Spec 054 §8) — fully or partially.
/// IdempotencyKey is required: the same key returns the existing receipt instead of
/// double-counting stock, so retries are safe. Omit ReceivedAt for now.</summary>
public record ReceiveGoodsRequest(
    string IdempotencyKey,
    List<ReceiveGoodsLineRequest> Lines,
    DateTime? ReceivedAt = null,
    string? Notes = null);
