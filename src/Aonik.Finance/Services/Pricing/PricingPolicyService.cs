using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Persistence;
using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Entities.Pricing;

namespace Aonik.Finance.Services.Pricing;

internal class PricingPolicyService : IPricingPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public PricingPolicyService(FinanceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<PricingPolicyResolution> ResolvePolicyAsync(
        PricingQuoteRequest request,
        string customerTier,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var policies = await _dbContext.FeePolicies
            .Where(policy => policy.IsActive)
            .ToListAsync(cancellationToken);

        var candidates = policies
            .Select(policy => new
            {
                Policy = policy,
                Conditions = ParseConditions(policy.ConditionsJson)
            })
            .Where(candidate => Matches(candidate.Conditions, request, customerTier))
            .Select(candidate => new
            {
                candidate.Policy,
                candidate.Conditions,
                Specificity = CalculateSpecificity(candidate.Conditions)
            })
            .OrderByDescending(candidate => candidate.Policy.TenantId == tenantId)
            .ThenByDescending(candidate => candidate.Specificity)
            .ThenByDescending(candidate => candidate.Policy.UpdatedAt ?? candidate.Policy.CreatedAt)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No pricing policy matched the requested corridor.");
        }

        var selected = candidates[0];
        var versionTimestamp = selected.Policy.UpdatedAt ?? selected.Policy.CreatedAt;
        var version = versionTimestamp == default
            ? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : versionTimestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new PricingPolicyResolution(selected.Policy, selected.Conditions, version);
    }

    public async Task<LimitsPolicy?> ResolveLimitsPolicyAsync(
        Guid? customerId,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var baseQuery = _dbContext.LimitsPolicies
            .Where(policy => policy.IsActive)
            .Where(policy => policy.Currency == currency);

        if (customerId.HasValue)
        {
            var customerPolicy = await baseQuery
                .Where(policy => policy.ScopeType == "Customer")
                .Where(policy => policy.ScopeId == customerId)
                .OrderByDescending(policy => policy.UpdatedAt ?? policy.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (customerPolicy != null)
            {
                return customerPolicy;
            }
        }

        return await baseQuery
            .Where(policy => policy.ScopeType == "Tenant")
            .Where(policy => policy.ScopeId == tenantId)
            .OrderByDescending(policy => policy.UpdatedAt ?? policy.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static FeePolicyConditions ParseConditions(string conditionsJson)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson))
        {
            return new FeePolicyConditions(null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        return JsonSerializer.Deserialize<FeePolicyConditions>(conditionsJson, JsonOptions)
            ?? new FeePolicyConditions(null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private static bool Matches(FeePolicyConditions conditions, PricingQuoteRequest request, string customerTier)
    {
        if (!MatchesValue(conditions.ServiceCode, request.ServiceCode))
            return false;

        if (!MatchesValue(conditions.OriginCountry, request.OriginCountry))
            return false;

        if (!MatchesValue(conditions.DestinationCountry, request.DestinationCountry))
            return false;

        if (!MatchesValue(conditions.OriginCurrency, request.OriginCurrency))
            return false;

        if (!MatchesValue(conditions.DestinationCurrency, request.DestinationCurrency))
            return false;

        if (!MatchesValue(conditions.CustomerTier, customerTier))
            return false;

        if (!MatchesTransferAmountBand(conditions, request))
            return false;

        return true;
    }

    private static bool MatchesTransferAmountBand(FeePolicyConditions conditions, PricingQuoteRequest request)
    {
        if (!conditions.MinTransferAmount.HasValue && !conditions.MaxTransferAmount.HasValue)
        {
            return true;
        }

        var transferAmount = request.OriginAmount ?? request.DestinationAmount;
        if (!transferAmount.HasValue)
        {
            return false;
        }

        if (conditions.MinTransferAmount.HasValue && transferAmount.Value < conditions.MinTransferAmount.Value)
        {
            return false;
        }

        if (conditions.MaxTransferAmount.HasValue && transferAmount.Value > conditions.MaxTransferAmount.Value)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesValue(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateSpecificity(FeePolicyConditions conditions)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(conditions.ServiceCode))
            score++;
        if (!string.IsNullOrWhiteSpace(conditions.OriginCountry))
            score++;
        if (!string.IsNullOrWhiteSpace(conditions.DestinationCountry))
            score++;
        if (!string.IsNullOrWhiteSpace(conditions.OriginCurrency))
            score++;
        if (!string.IsNullOrWhiteSpace(conditions.DestinationCurrency))
            score++;
        if (!string.IsNullOrWhiteSpace(conditions.CustomerTier))
            score++;
        if (conditions.MinTransferAmount.HasValue)
            score++;
        if (conditions.MaxTransferAmount.HasValue)
            score++;

        return score;
    }
}
