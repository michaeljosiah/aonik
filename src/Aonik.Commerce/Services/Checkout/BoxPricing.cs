using Aonik.Commerce.Entities.Catalog;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// The single implementation of Spec 068 §5 — used by the size-plan read, the cart quote and
/// checkout so the three can never disagree. Presets always win over the formula at their size;
/// the charge for growing a box is always boxPrice(target) − boxPrice(current), never
/// PerSpacePrice × spaces (around a discounted preset the marginal price of a space bends).
/// </summary>
internal static class BoxPricing
{
    public static bool IsValidSize(BundleSizePlan plan, int size)
        => size >= plan.MinSize && size <= plan.MaxSize;

    /// <summary>boxPrice(size): the preset price when a preset row exists for that size, else
    /// BasePrice + (size − BaseSize) × PerSpacePrice. Callers validate the size first.</summary>
    public static decimal BoxPrice(BundleSizePlan plan, int size)
    {
        var preset = plan.Presets.FirstOrDefault(p => p.Size == size && !p.IsDeleted);
        return preset?.Price ?? plan.BasePrice + (size - plan.BaseSize) * plan.PerSpacePrice;
    }
}
