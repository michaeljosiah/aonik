namespace Aonik.Commerce.Contracts.Models.Sourcing;

/// <summary>Sets a new effective-dated unit cost for an ingredient (Spec 051 §8), per the
/// ingredient's base unit, in one currency. A null <paramref name="EffectiveFrom"/> means "now";
/// a future date stores a <em>scheduled</em> row that does not price rollups until the date
/// arrives (R4). The prior open row is closed at <paramref name="EffectiveFrom"/> in the same
/// transaction — repricing is a historied action, never an overwrite (R2).</summary>
public record SetIngredientCostCommand(
    Guid IngredientId,
    string Currency,
    decimal UnitCost,
    DateTime? EffectiveFrom = null);

/// <summary>One effective-dated cost row (Spec 051 §7/§8). A null <paramref name="EffectiveTo"/>
/// marks the open row; which row is <em>current</em> on a date is resolved by the effective-date
/// window, not <paramref name="IsActive"/> (a convenience/soft-delete flag).</summary>
public record IngredientCostDto(
    Guid Id,
    Guid IngredientId,
    string Currency,
    decimal UnitCost,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive);
