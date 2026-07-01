namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>Creates an ingredient (raw material) in the tenant's master (Spec 050 §8).
/// Name — and SKU, where set — must be unique per tenant.</summary>
public record CreateIngredientCommand(
    string Name,
    string BaseUnit,
    string? Sku = null,
    string? Category = null,
    string? Notes = null);

/// <summary>Updates an ingredient's master data (Spec 050 §8/R1).</summary>
public record UpdateIngredientCommand(
    Guid IngredientId,
    string Name,
    string BaseUnit,
    string? Sku = null,
    string? Category = null,
    string? Notes = null,
    bool IsActive = true);

public record IngredientDto(
    Guid Id,
    string Name,
    string? Sku,
    string BaseUnit,
    string? Category,
    bool IsActive,
    string? Notes);
