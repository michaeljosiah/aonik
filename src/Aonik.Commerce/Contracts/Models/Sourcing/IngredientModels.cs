namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>Creates an ingredient (raw material) in the tenant's master (Spec 050 §8).
/// Name — and SKU, where set — must be unique per tenant.</summary>
public record CreateIngredientCommand(
    string Name,
    string BaseUnit,
    string? Sku = null,
    string? Category = null,
    string? Notes = null);

/// <summary>Updates an ingredient's master data (Spec 050 §8/R1). A null <paramref name="IsActive"/>
/// preserves the stored active state — an update that says nothing about the flag never silently
/// reactivates (or deactivates) an ingredient.</summary>
public record UpdateIngredientCommand(
    Guid IngredientId,
    string Name,
    string BaseUnit,
    string? Sku = null,
    string? Category = null,
    string? Notes = null,
    bool? IsActive = null);

public record IngredientDto(
    Guid Id,
    string Name,
    string? Sku,
    string BaseUnit,
    string? Category,
    bool IsActive,
    string? Notes);
