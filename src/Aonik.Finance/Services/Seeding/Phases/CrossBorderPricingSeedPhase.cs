using System.Text.Json;

using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

using Aonik.SharedKernel.Seeding;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds cross-border pricing: FX quotes for UK/USD→Africa corridors,
/// tiered fee policies for NG→GH/KE/ZA, and daily limits policies.
/// </summary>
internal sealed class CrossBorderPricingSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly PricingUpsertHelper _pricing;

    public CrossBorderPricingSeedPhase(
        FinanceDbContext db,
        PricingUpsertHelper pricing)
    {
        _db = db;
        _pricing = pricing;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;

        var fxQuoteIds = new List<Guid>
        {
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.Pricing.DemoFxQuoteId, "NGN", "GHS", 0.0076m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-GH" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.NgnKesFxQuoteId, "NGN", "KES", 0.083m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-KE" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.NgnZarFxQuoteId, "NGN", "ZAR", 0.011m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-ZA" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.UsdGhsFxQuoteId, "USD", "GHS", 12.85m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-GH" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.UsdKesFxQuoteId, "USD", "KES", 150.40m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-KE" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.UsdZarFxQuoteId, "USD", "ZAR", 18.60m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-ZA" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.GbpNgnFxQuoteId, "GBP", "NGN", 1985.25m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-NG" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.GbpGhsFxQuoteId, "GBP", "GHS", 16.78m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-GH" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.GbpKesFxQuoteId, "GBP", "KES", 168.45m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-KE" }), now, userId, cancellationToken),
            await _pricing.UpsertFxQuoteAsync(tenantId, SeedIds.CrossBorderFxQuotes.GbpZarFxQuoteId, "GBP", "ZAR", 24.31m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-ZA" }), now, userId, cancellationToken)
        };

        var breakdown = new List<FeeBreakdownDefinition>
        {
            new("SERVICE_FEE", "Service fee", "Fixed"),
            new("FX_MARKUP", "FX markup", "FxMarkup")
        };

        var feePolicyIds = new List<Guid>
        {
            await _pricing.UpsertFeePolicyAsync(tenantId, SeedIds.CrossBorderFeePolicies.CrossBorderBand1FeePolicyId, "CrossBorder-NG-GH-Band-001-100", 40m, 0.010m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 0m, 100m, 20m, 180m, 120, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await _pricing.UpsertFeePolicyAsync(tenantId, SeedIds.CrossBorderFeePolicies.CrossBorderBand2FeePolicyId, "CrossBorder-NG-GH-Band-100-1000", 85m, 0.012m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 100.01m, 1000m, 35m, 400m, 150, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await _pricing.UpsertFeePolicyAsync(tenantId, SeedIds.CrossBorderFeePolicies.CrossBorderBand3FeePolicyId, "CrossBorder-NG-GH-Band-1000-Plus", 150m, 0.015m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 1000.01m, null, 60m, 1250m, 180, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await _pricing.UpsertFeePolicyAsync(tenantId, SeedIds.CrossBorderFeePolicies.CrossBorderKesFeePolicyId, "CrossBorder-NG-KE-Default", 75m, 0.013m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID.KE.KPLC", "NG", "KE", "NGN", "KES", "Retail", null, null, 30m, 600m, 140, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await _pricing.UpsertFeePolicyAsync(tenantId, SeedIds.CrossBorderFeePolicies.CrossBorderZarFeePolicyId, "CrossBorder-NG-ZA-Default", 90m, 0.014m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID.ZA.CPJ", "NG", "ZA", "NGN", "ZAR", "Retail", null, null, 40m, 750m, 150, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken)
        };

        var limitsPolicyIds = new List<Guid>
        {
            await _pricing.UpsertLimitsPolicyAsync(tenantId, SeedIds.Pricing.DemoLimitsPolicyId, "NGN", 5000000m, "Daily", now, userId, cancellationToken),
            await _pricing.UpsertLimitsPolicyAsync(tenantId, SeedIds.CrossBorderLimitsPolicies.KenyaLimitsPolicyId, "KES", 300000m, "Daily", now, userId, cancellationToken),
            await _pricing.UpsertLimitsPolicyAsync(tenantId, SeedIds.CrossBorderLimitsPolicies.SouthAfricaLimitsPolicyId, "ZAR", 120000m, "Daily", now, userId, cancellationToken)
        };

        await _db.SaveChangesAsync(cancellationToken);

        results[DemoSeedResultKeys.CrossBorderFxQuoteIds] = fxQuoteIds;
        results[DemoSeedResultKeys.CrossBorderFeePolicyIds] = feePolicyIds;
        results[DemoSeedResultKeys.CrossBorderLimitsPolicyIds] = limitsPolicyIds;

        return new[] { "Seeded UK-to-Africa FX quotes and tiered cross-border charging policies" };
    }
}
