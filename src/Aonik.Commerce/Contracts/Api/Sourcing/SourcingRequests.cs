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
