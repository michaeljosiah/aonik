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
    bool IsActive = true);
