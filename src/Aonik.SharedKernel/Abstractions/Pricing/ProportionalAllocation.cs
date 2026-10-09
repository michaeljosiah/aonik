namespace Aonik.SharedKernel.Abstractions.Pricing;

public static class ProportionalAllocation
{
    // Retains reporting's 4dp rounding and largest-weight/first-row tie break, but spreads
    // a large negative remainder across rows instead of making one tiny line negative.
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights, bool capAtWeights = true)
    {
        if (total < 0 || weights.Any(weight => weight < 0)) throw new ArgumentException("Allocation amounts cannot be negative.");
        var shares = new decimal[weights.Count];
        if (weights.Count == 0 || total == 0) return shares;
        IReadOnlyList<decimal> effective = weights;
        var sum = weights.Sum();
        if (sum <= 0)
        {
            if (capAtWeights) throw new ArgumentException("A discount requires positive eligible goods.");
            effective = Enumerable.Repeat(1m, weights.Count).ToArray();
            sum = weights.Count;
        }
        if (capAtWeights && total > sum) throw new ArgumentException("A discount cannot exceed eligible goods.");
        for (var index = 0; index < shares.Length; index++)
        {
            shares[index] = Math.Round(total * effective[index] / sum, 4, MidpointRounding.AwayFromZero);
            if (capAtWeights) shares[index] = Math.Min(shares[index], weights[index]);
        }
        var remainder = total - shares.Sum();
        foreach (var index in Enumerable.Range(0, shares.Length).OrderByDescending(index => effective[index]))
        {
            if (remainder == 0) break;
            var adjustment = remainder < 0 ? -Math.Min(-remainder, shares[index])
                : capAtWeights ? Math.Min(remainder, weights[index] - shares[index]) : remainder;
            shares[index] += adjustment;
            remainder -= adjustment;
        }
        if (remainder != 0) throw new InvalidOperationException("Discount allocation did not reconcile.");
        return shares;
    }
}
