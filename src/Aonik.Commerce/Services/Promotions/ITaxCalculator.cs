namespace Aonik.Commerce.Services.Promotions;

/// <summary>
/// Computes tax on a taxable amount at checkout (Spec 042 §5 follow-up). A seam: the default
/// <see cref="ZeroRateTaxCalculator"/> charges no tax; deployments that need VAT/sales tax register
/// their own implementation (e.g. a jurisdiction-aware calculator) at the composition root.
/// </summary>
public interface ITaxCalculator
{
    Task<decimal> CalculateAsync(decimal taxableAmount, string currency, CancellationToken cancellationToken = default);
}

/// <summary>Default tax calculator — charges no tax. Replace at the composition root when needed.</summary>
internal sealed class ZeroRateTaxCalculator : ITaxCalculator
{
    public Task<decimal> CalculateAsync(decimal taxableAmount, string currency, CancellationToken cancellationToken = default)
        => Task.FromResult(0m);
}
