using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Entities.Pricing;

namespace Aonik.Finance.Contracts.Services.Pricing;

public interface IPricingPolicyService
{
    Task<PricingPolicyResolution> ResolvePolicyAsync(
        PricingQuoteRequest request,
        string customerTier,
        CancellationToken cancellationToken = default);

    Task<LimitsPolicy?> ResolveLimitsPolicyAsync(
        Guid? customerId,
        string currency,
        CancellationToken cancellationToken = default);
}

public record PricingPolicyResolution(
    FeePolicy Policy,
    FeePolicyConditions Conditions,
    string Version);
