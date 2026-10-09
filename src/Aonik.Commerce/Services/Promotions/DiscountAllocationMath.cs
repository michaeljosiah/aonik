using Aonik.SharedKernel.Abstractions.Pricing;

namespace Aonik.Commerce.Services.Promotions;

internal static class DiscountAllocationMath
{
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights, bool capAtWeights = true)
        => ProportionalAllocation.Allocate(total, weights, capAtWeights);
}
