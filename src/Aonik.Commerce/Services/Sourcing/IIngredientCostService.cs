using Aonik.Commerce.Contracts.Models.Sourcing;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Effective-dated ingredient unit costs for the Commerce module (Spec 051 §8), mirroring the
/// retail <c>ProductPrice</c> pattern. Repricing closes the prior open row and inserts a new one
/// in the same transaction, so history is append-only in effect; which row is <em>current</em> on
/// a date is resolved date-aware from the [EffectiveFrom, EffectiveTo) window — <c>IsActive</c> is
/// a convenience/soft-delete flag, never the selector. Spec 054 (goods receipt) reuses
/// <see cref="SetCostAsync"/> to record actual received costs.
/// </summary>
public interface IIngredientCostService
{
    /// <summary>
    /// Sets a new unit cost for an ingredient in a currency (Spec 051 §8/R2): closes the current
    /// open row at the new cost's <c>EffectiveFrom</c> and inserts the new open row, in one
    /// transaction. A future <c>EffectiveFrom</c> stores a <em>scheduled</em> row — the prior cost
    /// keeps pricing until the date arrives (R4). An <c>EffectiveFrom</c> earlier than the open
    /// row's start is rejected (it would invert the window and rewrite history).
    /// </summary>
    Task<IngredientCostDto> SetCostAsync(SetIngredientCostCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cost effective at <paramref name="atUtc"/> (default: now) — the row where
    /// <c>EffectiveFrom &lt;= atUtc AND (EffectiveTo IS NULL OR atUtc &lt; EffectiveTo)</c>,
    /// newest first (Spec 051 §8/R3). Date-aware: a scheduled (future-dated) row never prices
    /// "now" (R4). Null when no cost is effective at <paramref name="atUtc"/>.
    /// </summary>
    Task<IngredientCostDto?> GetCurrentCostAsync(Guid ingredientId, string currency, DateTime? atUtc = null, CancellationToken cancellationToken = default);

    /// <summary>The full reprice timeline for an ingredient, newest first, optionally filtered to
    /// one currency (Spec 051 §8/R1) — e.g. "₦1,100 → ₦1,200 on 2026-06-01".</summary>
    Task<IReadOnlyList<IngredientCostDto>> ListHistoryAsync(Guid ingredientId, string? currency = null, CancellationToken cancellationToken = default);
}
