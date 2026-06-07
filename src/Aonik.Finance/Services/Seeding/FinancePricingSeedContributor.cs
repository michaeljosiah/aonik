using System.Text.Json;

using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Seeds baseline remittance pricing data required by local/dev verification. Demo seed phases add a
/// broader catalog, but startup/migrator seed routines only run global seed contributors.
/// </summary>
internal sealed class FinancePricingSeedContributor : IGlobalSeedContributor
{
    private const string Provider = "DemoRate";
    private readonly FinanceDbContext _dbContext;
    private readonly DbContextOptions<FinanceDbContext> _dbContextOptions;
    private readonly IClock _clock;

    public FinancePricingSeedContributor(
        FinanceDbContext dbContext,
        DbContextOptions<FinanceDbContext> dbContextOptions,
        IClock clock)
    {
        _dbContext = dbContext;
        _dbContextOptions = dbContextOptions;
        _clock = clock;
    }

    public string Key => "FinancePricing";
    public string DisplayName => "Finance Pricing";
    public string Description => "Seeds baseline FX rates and remittance fee policies for local/dev verification.";
    public int SortOrder => 40;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _dbContext.Users
            .AcrossTenants()
            .AsNoTracking()
            .Select(user => user.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (tenantIds.Count == 0)
        {
            return ["No tenants found for finance pricing seed."];
        }

        foreach (var tenantId in tenantIds)
        {
            await using var tenantDb = new FinanceDbContext(
                _dbContextOptions,
                new FixedTenantProvider(tenantId),
                clock: _clock);

            await UpsertFxQuoteAsync(tenantDb, tenantId, "GBP", "NGN", 1985.25m, new { corridor = "UK-NG", seed = Key }, cancellationToken);
            await UpsertFxQuoteAsync(tenantDb, tenantId, "NGN", "NGN", 1m, new { corridor = "NG-NG", seed = Key }, cancellationToken);

            await UpsertFeePolicyAsync(
                tenantDb,
                tenantId,
                "Remittance-UK-NG-Default",
                fixedFee: 1m,
                percentageFee: 0.01m,
                new FeePolicyConditions(
                    "REMITTANCE.PAYOUT",
                    "GB",
                    "NG",
                    "GBP",
                    "NGN",
                    null,
                    null,
                    null,
                    1m,
                    25m,
                    120,
                    Provider,
                    "AwayFromZero",
                    FeeBreakdown()),
                cancellationToken);

            await UpsertFeePolicyAsync(
                tenantDb,
                tenantId,
                "Remittance-NG-NG-Default",
                fixedFee: 25m,
                percentageFee: 0.005m,
                new FeePolicyConditions(
                    "REMITTANCE.PAYOUT",
                    "NG",
                    "NG",
                    "NGN",
                    "NGN",
                    null,
                    null,
                    null,
                    25m,
                    500m,
                    0,
                    Provider,
                    "AwayFromZero",
                    FeeBreakdown()),
                cancellationToken);

            await tenantDb.SaveChangesAsync(cancellationToken);
        }

        return [$"Seeded remittance pricing for {tenantIds.Count} tenant(s)."];
    }

    private async Task UpsertFxQuoteAsync(
        FinanceDbContext dbContext,
        Guid tenantId,
        string baseCurrency,
        string targetCurrency,
        decimal rate,
        object metadata,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var quote = await dbContext.FxQuotes.FirstOrDefaultAsync(
            item => item.TenantId == tenantId
                    && item.BaseCurrency == baseCurrency
                    && item.TargetCurrency == targetCurrency
                    && item.Provider == Provider,
            cancellationToken);

        if (quote is null)
        {
            quote = new FxQuote
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BaseCurrency = baseCurrency,
                TargetCurrency = targetCurrency,
                Provider = Provider,
                CreatedAt = now
            };
            dbContext.FxQuotes.Add(quote);
        }

        quote.Rate = rate;
        quote.ExpiresAt = now.AddDays(365);
        quote.MetadataJson = JsonSerializer.Serialize(metadata);
        quote.UpdatedAt = now;
    }

    private async Task UpsertFeePolicyAsync(
        FinanceDbContext dbContext,
        Guid tenantId,
        string name,
        decimal fixedFee,
        decimal percentageFee,
        FeePolicyConditions conditions,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var policy = await dbContext.FeePolicies.FirstOrDefaultAsync(
            item => item.TenantId == tenantId && item.Name == name,
            cancellationToken);

        if (policy is null)
        {
            policy = new FeePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                CreatedAt = now
            };
            dbContext.FeePolicies.Add(policy);
        }

        policy.FixedFee = fixedFee;
        policy.PercentageFee = percentageFee;
        policy.ConditionsJson = JsonSerializer.Serialize(conditions);
        policy.IsActive = true;
        policy.UpdatedAt = now;
    }

    private static IReadOnlyCollection<FeeBreakdownDefinition> FeeBreakdown()
        =>
        [
            new("SERVICE_FEE", "Service fee", "Fixed"),
            new("FX_MARKUP", "FX markup", "FxMarkup")
        ];

    private sealed class FixedTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;

        public bool TryGetCurrentTenantId(out Guid resolvedTenantId)
        {
            resolvedTenantId = tenantId;
            return tenantId != Guid.Empty;
        }
    }
}
