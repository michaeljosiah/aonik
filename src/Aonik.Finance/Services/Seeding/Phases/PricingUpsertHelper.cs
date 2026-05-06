using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Shared upsert helpers for pricing entities (FX quotes, fee policies,
/// limits policies). Used by both <see cref="PricingSeedPhase"/> and
/// <see cref="CrossBorderPricingSeedPhase"/>.
/// </summary>
internal sealed class PricingUpsertHelper
{
    private readonly FinanceDbContext _db;

    public PricingUpsertHelper(FinanceDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> UpsertFxQuoteAsync(
        Guid tenantId,
        Guid fxQuoteId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        string provider,
        string metadataJson,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var fxQuote = await _db.FxQuotes
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.BaseCurrency == baseCurrency
                                         && item.TargetCurrency == targetCurrency
                                         && item.Provider == provider,
                cancellationToken);

        if (fxQuote == null)
        {
            fxQuote = new FxQuote
            {
                Id = fxQuoteId,
                TenantId = tenantId,
                BaseCurrency = baseCurrency,
                TargetCurrency = targetCurrency,
                Rate = rate,
                ExpiresAt = now.AddHours(24),
                Provider = provider,
                MetadataJson = metadataJson,
                CreatedAt = now,
                CreatedBy = userId
            };
            _db.FxQuotes.Add(fxQuote);
        }
        else
        {
            fxQuote.BaseCurrency = baseCurrency;
            fxQuote.TargetCurrency = targetCurrency;
            fxQuote.Rate = rate;
            fxQuote.ExpiresAt = now.AddHours(24);
            fxQuote.Provider = provider;
            fxQuote.MetadataJson = metadataJson;
            fxQuote.UpdatedAt = now;
            fxQuote.UpdatedBy = userId;
        }

        return fxQuote.Id;
    }

    public async Task<Guid> UpsertFeePolicyAsync(
        Guid tenantId,
        Guid feePolicyId,
        string name,
        decimal fixedFee,
        decimal percentageFee,
        FeePolicyConditions conditions,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var feePolicy = await _db.FeePolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == name, cancellationToken);

        var conditionsJson = JsonSerializer.Serialize(conditions);

        if (feePolicy == null)
        {
            feePolicy = new FeePolicy
            {
                Id = feePolicyId,
                TenantId = tenantId,
                Name = name,
                FixedFee = fixedFee,
                PercentageFee = percentageFee,
                ConditionsJson = conditionsJson,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _db.FeePolicies.Add(feePolicy);
        }
        else
        {
            feePolicy.Name = name;
            feePolicy.FixedFee = fixedFee;
            feePolicy.PercentageFee = percentageFee;
            feePolicy.ConditionsJson = conditionsJson;
            feePolicy.IsActive = true;
            feePolicy.UpdatedAt = now;
            feePolicy.UpdatedBy = userId;
        }

        return feePolicy.Id;
    }

    public async Task<Guid> UpsertLimitsPolicyAsync(
        Guid tenantId,
        Guid limitsPolicyId,
        string currency,
        decimal maxAmount,
        string period,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var limitsPolicy = await _db.LimitsPolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ScopeType == "Tenant"
                                         && item.ScopeId == tenantId
                                         && item.Currency == currency
                                         && item.Period == period,
                cancellationToken);

        if (limitsPolicy == null)
        {
            limitsPolicy = new LimitsPolicy
            {
                Id = limitsPolicyId,
                TenantId = tenantId,
                ScopeType = "Tenant",
                ScopeId = tenantId,
                Currency = currency,
                MaxAmount = maxAmount,
                Period = period,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _db.LimitsPolicies.Add(limitsPolicy);
        }
        else
        {
            limitsPolicy.ScopeType = "Tenant";
            limitsPolicy.ScopeId = tenantId;
            limitsPolicy.Currency = currency;
            limitsPolicy.MaxAmount = maxAmount;
            limitsPolicy.Period = period;
            limitsPolicy.IsActive = true;
            limitsPolicy.UpdatedAt = now;
            limitsPolicy.UpdatedBy = userId;
        }

        return limitsPolicy.Id;
    }
}
