namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>Registers a supplier (Spec 053 §9). Name must be unique per tenant;
/// <paramref name="Currency"/> is the ISO 4217 code we buy from this supplier in.
/// <paramref name="PartyId"/> optionally soft-links a platform counterparty Party (no FK).</summary>
public record CreateSupplierCommand(
    string Name,
    string Currency,
    Guid? PartyId = null,
    int? LeadTimeDays = null,
    string? PaymentTerms = null);

/// <summary>Updates a supplier's master data (Spec 053 §9). A null <paramref name="IsActive"/>
/// preserves the stored active state — an update that says nothing about the flag never silently
/// reactivates (or deactivates) a supplier.</summary>
public record UpdateSupplierCommand(
    Guid SupplierId,
    string Name,
    string Currency,
    Guid? PartyId = null,
    int? LeadTimeDays = null,
    string? PaymentTerms = null,
    bool? IsActive = null);

public record SupplierDto(
    Guid Id,
    string Name,
    string Currency,
    Guid? PartyId,
    int? LeadTimeDays,
    string? PaymentTerms,
    bool IsActive);

/// <summary>Upserts one supplier price-list row (Spec 053 §9) — keyed by (supplier, ingredient).
/// <paramref name="PackSize"/> is the buy-side conversion: how many of the ingredient's base unit
/// one pack contains (25 for a 25 kg sack of a kg-based ingredient). A null
/// <paramref name="Currency"/> defaults to the supplier's currency.</summary>
public record UpsertSupplierIngredientCommand(
    Guid SupplierId,
    Guid IngredientId,
    decimal PackSize,
    decimal PackPrice,
    string? Currency = null,
    string? Sku = null,
    int? LeadTimeDays = null);

/// <summary>One supplier catalog row (Spec 053 §9). <paramref name="UnitPrice"/> is the derived
/// per-base-unit price (<c>PackPrice / PackSize</c>) — the default a PO line prices at (§10).</summary>
public record SupplierIngredientDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    Guid IngredientId,
    string? IngredientName,
    string? IngredientBaseUnit,
    string? Sku,
    decimal PackSize,
    decimal PackPrice,
    decimal UnitPrice,
    string Currency,
    int? LeadTimeDays);
