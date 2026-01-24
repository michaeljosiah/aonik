using Aonik.Application.Models.Pricing;
using Aonik.Domain.Pricing.Entities;

namespace Aonik.Application.Services.Pricing;

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
