using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Pricing;
using Aonik.Domain.Pricing.Entities;

namespace Aonik.Application.Services.Pricing;

public class PricingPolicyService : IPricingPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public PricingPolicyService(IAonikDbContext dbContext, ITenantProvider tenantProvider)
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
            .OrderByDescending(candidate => candidate.Policy.TenantId == tenantId)
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
        if (!customerId.HasValue)
        {
            return null;
        }

        return await _dbContext.LimitsPolicies
            .Where(policy => policy.IsActive)
            .Where(policy => policy.ScopeType == "Customer")
            .Where(policy => policy.ScopeId == customerId)
            .Where(policy => policy.Currency == currency)
            .OrderByDescending(policy => policy.UpdatedAt ?? policy.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static FeePolicyConditions ParseConditions(string conditionsJson)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson))
        {
            return new FeePolicyConditions(null, null, null, null, null, null, null, null, null, null, null, null);
        }

        return JsonSerializer.Deserialize<FeePolicyConditions>(conditionsJson, JsonOptions)
            ?? new FeePolicyConditions(null, null, null, null, null, null, null, null, null, null, null, null);
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
}
