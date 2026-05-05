using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Finance module's demo-seed contributor. Handles all Finance-domain seeding
/// (partners, catalog, pricing, households) that was previously embedded in
/// <c>DemoSeedService</c> and <c>CatalogSeedService</c> in the Platform module.
/// </summary>
internal sealed class FinanceDemoSeedContributor : IDemoSeedContributor
{
    private const string PrefundAccountRole = "PrefundAsset";
    private const string PartnerPrefundSeedSourceType = "PartnerPrefundSeed";

    private readonly FinanceDbContext _financeDbContext;
    private readonly ILogger<FinanceDemoSeedContributor> _logger;

    // Accumulated results that the orchestrator can read via GetResults()
    private readonly Dictionary<string, object> _results = new();

    // ── Well-known Guid constants ────────────────────────────────────
    //
    // All deterministic GUIDs live in finance-demo-ids.json (embedded
    // resource) and are exposed here as static field aliases so the rest
    // of the file reads naturally. The JSON is loaded once per process
    // via FinanceDemoSeedIds.Instance.

    #region Static Guid constants

    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    // Catalog
    private static readonly Guid UtilitiesCategoryId = SeedIds.Catalog.UtilitiesCategoryId;
    private static readonly Guid EcgBillerId = SeedIds.Catalog.EcgBillerId;
    private static readonly Guid GhanaWaterBillerId = SeedIds.Catalog.GhanaWaterBillerId;
    private static readonly Guid EcgPrepaidServiceId = SeedIds.Catalog.EcgPrepaidServiceId;
    private static readonly Guid GhanaWaterServiceId = SeedIds.Catalog.GhanaWaterServiceId;
    private static readonly Guid EcgPostpaidServiceId = SeedIds.Catalog.EcgPostpaidServiceId;
    private static readonly Guid GhanaWaterPrepaidServiceId = SeedIds.Catalog.GhanaWaterPrepaidServiceId;

    // Pricing
    private static readonly Guid DemoFxQuoteId = SeedIds.Pricing.DemoFxQuoteId;
    private static readonly Guid DemoFeePolicyId = SeedIds.Pricing.DemoFeePolicyId;
    private static readonly Guid DemoLimitsPolicyId = SeedIds.Pricing.DemoLimitsPolicyId;

    // Cross-border categories
    private static readonly Guid NigeriaUtilitiesCategoryId = SeedIds.CrossBorderCategories.NigeriaUtilitiesCategoryId;
    private static readonly Guid KenyaUtilitiesCategoryId = SeedIds.CrossBorderCategories.KenyaUtilitiesCategoryId;
    private static readonly Guid SouthAfricaUtilitiesCategoryId = SeedIds.CrossBorderCategories.SouthAfricaUtilitiesCategoryId;

    // Cross-border billers
    private static readonly Guid IkejaElectricBillerId = SeedIds.CrossBorderBillers.IkejaElectricBillerId;
    private static readonly Guid LagosWaterBillerId = SeedIds.CrossBorderBillers.LagosWaterBillerId;
    private static readonly Guid KenyaPowerBillerId = SeedIds.CrossBorderBillers.KenyaPowerBillerId;
    private static readonly Guid CityPowerBillerId = SeedIds.CrossBorderBillers.CityPowerBillerId;

    // Cross-border services
    private static readonly Guid IkejaPrepaidServiceId = SeedIds.CrossBorderServices.IkejaPrepaidServiceId;
    private static readonly Guid IkejaPostpaidServiceId = SeedIds.CrossBorderServices.IkejaPostpaidServiceId;
    private static readonly Guid LagosWaterServiceId = SeedIds.CrossBorderServices.LagosWaterServiceId;
    private static readonly Guid LagosWaterPrepaidServiceId = SeedIds.CrossBorderServices.LagosWaterPrepaidServiceId;
    private static readonly Guid KenyaPowerServiceId = SeedIds.CrossBorderServices.KenyaPowerServiceId;
    private static readonly Guid KenyaPowerPostpaidServiceId = SeedIds.CrossBorderServices.KenyaPowerPostpaidServiceId;
    private static readonly Guid CityPowerServiceId = SeedIds.CrossBorderServices.CityPowerServiceId;
    private static readonly Guid CityPowerPostpaidServiceId = SeedIds.CrossBorderServices.CityPowerPostpaidServiceId;

    // Partners
    private static readonly Guid NigeriaPartnerId = SeedIds.Partners.NigeriaPartnerId;
    private static readonly Guid GhanaPartnerId = SeedIds.Partners.GhanaPartnerId;
    private static readonly Guid KenyaPartnerId = SeedIds.Partners.KenyaPartnerId;
    private static readonly Guid SouthAfricaPartnerId = SeedIds.Partners.SouthAfricaPartnerId;

    // Branches
    private static readonly Guid NigeriaBranchId = SeedIds.Branches.NigeriaBranchId;
    private static readonly Guid GhanaBranchId = SeedIds.Branches.GhanaBranchId;
    private static readonly Guid KenyaBranchId = SeedIds.Branches.KenyaBranchId;
    private static readonly Guid SouthAfricaBranchId = SeedIds.Branches.SouthAfricaBranchId;

    // Connectors
    private static readonly Guid NigeriaConnectorId = SeedIds.Connectors.NigeriaConnectorId;
    private static readonly Guid GhanaConnectorId = SeedIds.Connectors.GhanaConnectorId;
    private static readonly Guid KenyaConnectorId = SeedIds.Connectors.KenyaConnectorId;
    private static readonly Guid SouthAfricaConnectorId = SeedIds.Connectors.SouthAfricaConnectorId;

    // Routing rules
    private static readonly Guid NigeriaRoutingRuleId = SeedIds.RoutingRules.NigeriaRoutingRuleId;
    private static readonly Guid GhanaRoutingRuleId = SeedIds.RoutingRules.GhanaRoutingRuleId;
    private static readonly Guid KenyaRoutingRuleId = SeedIds.RoutingRules.KenyaRoutingRuleId;
    private static readonly Guid SouthAfricaRoutingRuleId = SeedIds.RoutingRules.SouthAfricaRoutingRuleId;

    // Households
    private static readonly Guid FamilyHouseholdId = SeedIds.Households.FamilyHouseholdId;
    private static readonly Guid ProfessionalsHouseholdId = SeedIds.Households.ProfessionalsHouseholdId;
    private static readonly Guid FamilyHouseholdMemberId = SeedIds.Households.FamilyHouseholdMemberId;
    private static readonly Guid ProfessionalsHouseholdMemberId = SeedIds.Households.ProfessionalsHouseholdMemberId;

    // Cross-border FX quotes
    private static readonly Guid NgnKesFxQuoteId = SeedIds.CrossBorderFxQuotes.NgnKesFxQuoteId;
    private static readonly Guid NgnZarFxQuoteId = SeedIds.CrossBorderFxQuotes.NgnZarFxQuoteId;
    private static readonly Guid UsdGhsFxQuoteId = SeedIds.CrossBorderFxQuotes.UsdGhsFxQuoteId;
    private static readonly Guid UsdKesFxQuoteId = SeedIds.CrossBorderFxQuotes.UsdKesFxQuoteId;
    private static readonly Guid UsdZarFxQuoteId = SeedIds.CrossBorderFxQuotes.UsdZarFxQuoteId;
    private static readonly Guid GbpNgnFxQuoteId = SeedIds.CrossBorderFxQuotes.GbpNgnFxQuoteId;
    private static readonly Guid GbpGhsFxQuoteId = SeedIds.CrossBorderFxQuotes.GbpGhsFxQuoteId;
    private static readonly Guid GbpKesFxQuoteId = SeedIds.CrossBorderFxQuotes.GbpKesFxQuoteId;
    private static readonly Guid GbpZarFxQuoteId = SeedIds.CrossBorderFxQuotes.GbpZarFxQuoteId;

    // Cross-border fee policies
    private static readonly Guid CrossBorderBand1FeePolicyId = SeedIds.CrossBorderFeePolicies.CrossBorderBand1FeePolicyId;
    private static readonly Guid CrossBorderBand2FeePolicyId = SeedIds.CrossBorderFeePolicies.CrossBorderBand2FeePolicyId;
    private static readonly Guid CrossBorderBand3FeePolicyId = SeedIds.CrossBorderFeePolicies.CrossBorderBand3FeePolicyId;
    private static readonly Guid CrossBorderKesFeePolicyId = SeedIds.CrossBorderFeePolicies.CrossBorderKesFeePolicyId;
    private static readonly Guid CrossBorderZarFeePolicyId = SeedIds.CrossBorderFeePolicies.CrossBorderZarFeePolicyId;

    // Cross-border limits policies
    private static readonly Guid KenyaLimitsPolicyId = SeedIds.CrossBorderLimitsPolicies.KenyaLimitsPolicyId;
    private static readonly Guid SouthAfricaLimitsPolicyId = SeedIds.CrossBorderLimitsPolicies.SouthAfricaLimitsPolicyId;

    // CatalogSeedService global biller categories (TenantId = Guid.Empty)
    private static readonly Guid GlobalUtilitiesCategoryId = SeedIds.GlobalCategories.GlobalUtilitiesCategoryId;
    private static readonly Guid GlobalTelecomCategoryId = SeedIds.GlobalCategories.GlobalTelecomCategoryId;
    private static readonly Guid GlobalInternetCategoryId = SeedIds.GlobalCategories.GlobalInternetCategoryId;
    private static readonly Guid GlobalEducationCategoryId = SeedIds.GlobalCategories.GlobalEducationCategoryId;
    private static readonly Guid GlobalGovernmentCategoryId = SeedIds.GlobalCategories.GlobalGovernmentCategoryId;
    private static readonly Guid GlobalCableCategoryId = SeedIds.GlobalCategories.GlobalCableCategoryId;

    #endregion

    // Internal state held between phases
    private Guid _billCollectionPartnerId;

    public FinanceDemoSeedContributor(
        FinanceDbContext financeDbContext,
        ILogger<FinanceDemoSeedContributor> logger)
    {
        _financeDbContext = financeDbContext;
        _logger = logger;
    }

    public string ModuleName => "Finance";

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedPhase phase,
        DemoSeedContext context,
        CancellationToken cancellationToken = default)
    {
        return phase switch
        {
            DemoSeedPhase.CatalogCategories => await SeedCatalogCategoriesAsync(cancellationToken),
            DemoSeedPhase.BillCollectionPartner => await SeedBillCollectionPartnerAsync(context, cancellationToken),
            DemoSeedPhase.Catalog => await SeedCatalogAsync(context, cancellationToken),
            DemoSeedPhase.Pricing => await SeedPricingAsync(context, cancellationToken),
            DemoSeedPhase.CrossBorderPartnerNetwork => await SeedCrossBorderPartnerNetworkAsync(context, cancellationToken),
            DemoSeedPhase.CrossBorderCatalog => await SeedCrossBorderCatalogAsync(context, cancellationToken),
            DemoSeedPhase.Households => await SeedHouseholdsAsync(context, cancellationToken),
            DemoSeedPhase.CrossBorderPricing => await SeedCrossBorderPricingAsync(context, cancellationToken),
            DemoSeedPhase.Activity => await SeedOrderActivityAsync(context, cancellationToken),
            _ => Array.Empty<string>()
        };
    }

    public void ClearTracking()
    {
        _financeDbContext.ChangeTracker.Clear();
    }

    public IReadOnlyDictionary<string, object> GetResults() => _results;

    // ── Phase: CatalogCategories ─────────────────────────────────────
    // Moved from CatalogSeedService.SeedCategoriesAsync

    private async Task<IReadOnlyList<string>> SeedCatalogCategoriesAsync(CancellationToken cancellationToken)
    {
        // Map category Name → deterministic GUID. The descriptive fields
        // (description, icon URL, country, sort) live in the embedded JSON;
        // this lookup pairs each record with its well-known ID.
        var idByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["Utilities"]  = GlobalUtilitiesCategoryId,
            ["Telecom"]    = GlobalTelecomCategoryId,
            ["Internet"]   = GlobalInternetCategoryId,
            ["Education"]  = GlobalEducationCategoryId,
            ["Government"] = GlobalGovernmentCategoryId,
            ["Cable"]      = GlobalCableCategoryId,
        };

        var categories = FinanceDemoSeedCatalog.Instance.GlobalCategories
            .Select(record => new CatalogBillerCategory
            {
                Id = idByName[record.Name],
                TenantId = Guid.Empty,
                CountryCode = record.CountryCode,
                Name = record.Name,
                Description = record.Description,
                IconUrl = record.IconUrl,
                SortOrder = record.SortOrder,
                IsActive = true,
            })
            .ToList();

        var existingIds = await _financeDbContext.CatalogBillerCategories
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<Guid>(existingIds);
        var toAdd = categories.Where(category => !existingSet.Contains(category.Id)).ToList();

        if (toAdd.Count == 0)
        {
            return Array.Empty<string>();
        }

        await _financeDbContext.CatalogBillerCategories.AddRangeAsync(toAdd, cancellationToken);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} biller categories", toAdd.Count);

        return new[] { $"Seeded {toAdd.Count} biller categories" };
    }

    // ── Phase: BillCollectionPartner ──────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedBillCollectionPartnerAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;
        const string partnerName = "Gold Coast Bill Hub";

        var partner = await _financeDbContext.Partners
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == partnerName, cancellationToken);

        var capabilitiesJson = JsonSerializer.Serialize(new[] { "BILLPAY", "COLLECTIONS" });
        var operatingHoursJson = JsonSerializer.Serialize(new
        {
            timezone = "Africa/Accra",
            weekdays = "06:00-22:00",
            weekends = "08:00-20:00"
        });

        if (partner == null)
        {
            partner = new Partner
            {
                Id = GhanaPartnerId,
                TenantId = tenantId,
                Name = partnerName,
                Status = "Active",
                CapabilitiesJson = capabilitiesJson,
                OperatingHoursJson = operatingHoursJson,
                CreatedAt = now,
                CreatedBy = userId
            };

            _financeDbContext.Partners.Add(partner);
        }
        else
        {
            partner.Status = "Active";
            partner.CapabilitiesJson = capabilitiesJson;
            partner.OperatingHoursJson = operatingHoursJson;
            partner.UpdatedAt = now;
            partner.UpdatedBy = userId;
        }

        await EnsurePartnerPrefundAccountAsync(
            tenantId,
            partner.Id,
            partner.Name,
            "GHS",
            90000m,
            now,
            userId,
            cancellationToken);

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        _billCollectionPartnerId = partner.Id;
        _results[DemoSeedResultKeys.BillCollectionPartnerId] = partner.Id;

        return new[] { "Ensured BillCollection GH partner and prefund account" };
    }

    // ── Phase: Catalog ───────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedCatalogAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;
        var operations = new List<string>();

        var category = await _financeDbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == "GH"
                                         && item.Name == "Utilities",
                cancellationToken);

        if (category == null)
        {
            category = new CatalogBillerCategory
            {
                Id = UtilitiesCategoryId,
                TenantId = tenantId,
                CountryCode = "GH",
                Name = "Utilities",
                Description = "Electricity and water billers",
                SortOrder = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _financeDbContext.CatalogBillerCategories.Add(category);
            operations.Add("Catalog category seeded");
        }
        else
        {
            category.Description = "Electricity and water billers";
            category.SortOrder = 1;
            category.IsActive = true;
            category.UpdatedAt = now;
            category.UpdatedBy = userId;
        }

        var categoryId = category.Id;
        var ecgBillerId = await UpsertBillerAsync(
            tenantId,
            categoryId,
            EcgBillerId,
            "ECG Power",
            "Ghana's electricity provider.",
            now,
            userId,
            operations,
            cancellationToken,
            _billCollectionPartnerId);

        var waterBillerId = await UpsertBillerAsync(
            tenantId,
            categoryId,
            GhanaWaterBillerId,
            "Ghana Water",
            "National water utility.",
            now,
            userId,
            operations,
            cancellationToken,
            _billCollectionPartnerId);

        var ecgServiceId = await UpsertServiceAsync(
            tenantId,
            ecgBillerId,
            EcgPrepaidServiceId,
            "BILLPAY.ELECTRICITY.PREPAID",
            "ECG Prepaid Electricity",
            "Prepaid",
            "GHS",
            5,
            500,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{EcgBillerId}/services/{EcgPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        var waterServiceId = await UpsertServiceAsync(
            tenantId,
            waterBillerId,
            GhanaWaterServiceId,
            "BILLPAY.WATER.POSTPAID",
            "Ghana Water Postpaid",
            "Postpaid",
            "GHS",
            10,
            1000,
            false,
            false,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 6, 20, null, "Enter account number", null)
            }),
            null,
            operations,
            cancellationToken);

        await UpsertServiceAsync(
            tenantId,
            ecgBillerId,
            EcgPostpaidServiceId,
            "BILLPAY.ELECTRICITY.POSTPAID.GH.ECG",
            "ECG Postpaid Electricity",
            "Postpaid",
            "GHS",
            20,
            2000,
            false,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{EcgBillerId}/services/{EcgPostpaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        await UpsertServiceAsync(
            tenantId,
            waterBillerId,
            GhanaWaterPrepaidServiceId,
            "BILLPAY.WATER.PREPAID.GH.GWL",
            "Ghana Water Prepaid",
            "Prepaid",
            "GHS",
            5,
            1000,
            true,
            true,
            BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{GhanaWaterBillerId}/services/{GhanaWaterPrepaidServiceId}/validate",
                "precheck")),
            operations,
            cancellationToken);

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        _results[DemoSeedResultKeys.UtilitiesCategoryId] = categoryId;
        _results[DemoSeedResultKeys.EcgBillerId] = ecgBillerId;
        _results[DemoSeedResultKeys.WaterBillerId] = waterBillerId;
        _results[DemoSeedResultKeys.EcgServiceId] = ecgServiceId;
        _results[DemoSeedResultKeys.WaterServiceId] = waterServiceId;

        return operations;
    }

    // ── Phase: Pricing ───────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedPricingAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;
        var operations = new List<string>();

        var fxQuote = await _financeDbContext.FxQuotes
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.BaseCurrency == "NGN"
                                         && item.TargetCurrency == "GHS"
                                         && item.Provider == "DemoRate",
                cancellationToken);
        var fxExpiresAt = now.AddHours(24);

        if (fxQuote == null)
        {
            fxQuote = new FxQuote
            {
                Id = DemoFxQuoteId,
                TenantId = tenantId,
                BaseCurrency = "NGN",
                TargetCurrency = "GHS",
                Rate = 0.0075m,
                ExpiresAt = fxExpiresAt,
                Provider = "DemoRate",
                MetadataJson = "{}",
                CreatedAt = now,
                CreatedBy = userId
            };
            _financeDbContext.FxQuotes.Add(fxQuote);
            operations.Add("Seeded FX quote");
        }
        else
        {
            fxQuote.Rate = 0.0075m;
            fxQuote.ExpiresAt = fxExpiresAt;
            fxQuote.Provider = "DemoRate";
            fxQuote.UpdatedAt = now;
            fxQuote.UpdatedBy = userId;
        }

        var conditions = new FeePolicyConditions(
            "BILLPAY.ELECTRICITY.PREPAID",
            "NG",
            "GH",
            "NGN",
            "GHS",
            "Retail",
            null,
            null,
            25m,
            500m,
            150,
            "DemoRate",
            "AwayFromZero",
            new List<FeeBreakdownDefinition>
            {
                new("SERVICE_FEE", "Service fee", "Fixed"),
                new("FX_MARKUP", "FX markup", "FxMarkup")
            });

        var feePolicy = await _financeDbContext.FeePolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.Name == "BillPay-NG-GH-Default",
                cancellationToken);

        if (feePolicy == null)
        {
            feePolicy = new FeePolicy
            {
                Id = DemoFeePolicyId,
                TenantId = tenantId,
                Name = "BillPay-NG-GH-Default",
                FixedFee = 50m,
                PercentageFee = 0.015m,
                ConditionsJson = JsonSerializer.Serialize(conditions),
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _financeDbContext.FeePolicies.Add(feePolicy);
            operations.Add("Seeded fee policy");
        }
        else
        {
            feePolicy.Name = "BillPay-NG-GH-Default";
            feePolicy.FixedFee = 50m;
            feePolicy.PercentageFee = 0.015m;
            feePolicy.ConditionsJson = JsonSerializer.Serialize(conditions);
            feePolicy.IsActive = true;
            feePolicy.UpdatedAt = now;
            feePolicy.UpdatedBy = userId;
        }

        var limitsPolicy = await _financeDbContext.LimitsPolicies
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ScopeType == "Tenant"
                                         && item.ScopeId == tenantId
                                         && item.Currency == "NGN"
                                         && item.Period == "Daily",
                cancellationToken);

        if (limitsPolicy == null)
        {
            limitsPolicy = new LimitsPolicy
            {
                Id = DemoLimitsPolicyId,
                TenantId = tenantId,
                ScopeType = "Tenant",
                ScopeId = tenantId,
                Currency = "NGN",
                MaxAmount = 1000000m,
                Period = "Daily",
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _financeDbContext.LimitsPolicies.Add(limitsPolicy);
            operations.Add("Seeded limits policy");
        }
        else
        {
            limitsPolicy.ScopeType = "Tenant";
            limitsPolicy.ScopeId = tenantId;
            limitsPolicy.Currency = "NGN";
            limitsPolicy.MaxAmount = 1000000m;
            limitsPolicy.Period = "Daily";
            limitsPolicy.IsActive = true;
            limitsPolicy.UpdatedAt = now;
            limitsPolicy.UpdatedBy = userId;
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        _results[DemoSeedResultKeys.FxQuoteId] = fxQuote.Id;
        _results[DemoSeedResultKeys.FeePolicyId] = feePolicy.Id;
        _results[DemoSeedResultKeys.LimitsPolicyId] = limitsPolicy.Id;

        return operations;
    }

    // ── Phase: CrossBorderPartnerNetwork ──────────────────────────────

    private sealed record PartnerRouteSeed(
        string CountryCode,
        Guid PartnerId,
        Guid BranchId,
        Guid ConnectorId,
        Guid RoutingRuleId,
        string PartnerName,
        string City,
        string BranchName,
        int Priority,
        string CurrencyCode,
        decimal OpeningPrefundBalance);

    private async Task<IReadOnlyList<string>> SeedCrossBorderPartnerNetworkAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;

        var seeds = new List<PartnerRouteSeed>
        {
            new("NG", NigeriaPartnerId, NigeriaBranchId, NigeriaConnectorId, NigeriaRoutingRuleId, "Naija Utility Switch", "Lagos", "Lagos Operations Hub", 10, "NGN", 3500000m),
            new("GH", GhanaPartnerId, GhanaBranchId, GhanaConnectorId, GhanaRoutingRuleId, "Gold Coast Bill Hub", "Accra", "Accra Settlement Hub", 20, "GHS", 90000m),
            new("KE", KenyaPartnerId, KenyaBranchId, KenyaConnectorId, KenyaRoutingRuleId, "EastPay Kenya", "Nairobi", "Nairobi Operations Hub", 30, "KES", 1800000m),
            new("ZA", SouthAfricaPartnerId, SouthAfricaBranchId, SouthAfricaConnectorId, SouthAfricaRoutingRuleId, "Mzansi Bill Connect", "Johannesburg", "Johannesburg Network Hub", 40, "ZAR", 320000m)
        };

        var partnerIdsByCountry = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var connectorIdsByCountry = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            var partner = await _financeDbContext.Partners
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == seed.PartnerName, cancellationToken);

            var capabilitiesJson = JsonSerializer.Serialize(new[] { "BILLPAY", "PAYOUT", "COLLECTIONS" });
            var operatingHoursJson = JsonSerializer.Serialize(new
            {
                timezone = "Africa/Lagos",
                weekdays = "06:00-22:00",
                weekends = "08:00-20:00"
            });

            if (partner == null)
            {
                partner = new Partner
                {
                    Id = seed.PartnerId,
                    TenantId = tenantId,
                    Name = seed.PartnerName,
                    Status = "Active",
                    CapabilitiesJson = capabilitiesJson,
                    OperatingHoursJson = operatingHoursJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _financeDbContext.Partners.Add(partner);
            }
            else
            {
                partner.Name = seed.PartnerName;
                partner.Status = "Active";
                partner.CapabilitiesJson = capabilitiesJson;
                partner.OperatingHoursJson = operatingHoursJson;
                partner.UpdatedAt = now;
                partner.UpdatedBy = userId;
            }

            var partnerId = partner.Id;

            await EnsurePartnerPrefundAccountAsync(
                tenantId,
                partnerId,
                seed.PartnerName,
                seed.CurrencyCode,
                seed.OpeningPrefundBalance,
                now,
                userId,
                cancellationToken);

            var branch = await _financeDbContext.PartnerBranches
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.PartnerId == partnerId
                                             && item.Name == seed.BranchName,
                    cancellationToken);

            var metadataJson = JsonSerializer.Serialize(new
            {
                timezone = "Africa/Lagos",
                supportsBillPay = true,
                settlementWindow = "T+0"
            });

            if (branch == null)
            {
                branch = new PartnerBranch
                {
                    Id = seed.BranchId,
                    TenantId = tenantId,
                    PartnerId = partnerId,
                    Name = seed.BranchName,
                    Country = seed.CountryCode,
                    City = seed.City,
                    MetadataJson = metadataJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _financeDbContext.PartnerBranches.Add(branch);
            }
            else
            {
                branch.Name = seed.BranchName;
                branch.Country = seed.CountryCode;
                branch.City = seed.City;
                branch.MetadataJson = metadataJson;
                branch.UpdatedAt = now;
                branch.UpdatedBy = userId;
            }

            var connector = await _financeDbContext.Connectors
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.PartnerId == partnerId
                                             && item.ConnectorType == "API",
                    cancellationToken);

            var connectorConfigJson = JsonSerializer.Serialize(new
            {
                endpoint = $"https://api.{seed.CountryCode.ToLowerInvariant()}.demo.aonik/connectors/billpay",
                retryPolicy = "ExponentialBackoff",
                timeoutSeconds = 30
            });

            if (connector == null)
            {
                connector = new Connector
                {
                    Id = seed.ConnectorId,
                    TenantId = tenantId,
                    PartnerId = partnerId,
                    ConnectorType = "API",
                    CredentialsRef = $"kv://demo/partners/{seed.CountryCode.ToLowerInvariant()}/api",
                    ConfigJson = connectorConfigJson,
                    Status = "Active",
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _financeDbContext.Connectors.Add(connector);
            }
            else
            {
                connector.ConnectorType = "API";
                connector.CredentialsRef = $"kv://demo/partners/{seed.CountryCode.ToLowerInvariant()}/api";
                connector.ConfigJson = connectorConfigJson;
                connector.Status = "Active";
                connector.UpdatedAt = now;
                connector.UpdatedBy = userId;
            }

            var connectorId = connector.Id;

            var routingRule = await _financeDbContext.RoutingRules
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.RoutingRuleId, cancellationToken);

            var conditionsJson = JsonSerializer.Serialize(new
            {
                quoteContext = "BillPayment",
                capability = "BILLPAY",
                destinationCountry = seed.CountryCode
            });

            if (routingRule == null)
            {
                routingRule = new RoutingRule
                {
                    Id = seed.RoutingRuleId,
                    TenantId = tenantId,
                    ConditionsJson = conditionsJson,
                    TargetPartnerId = partnerId,
                    TargetConnectorId = connectorId,
                    Priority = seed.Priority,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _financeDbContext.RoutingRules.Add(routingRule);
            }
            else
            {
                routingRule.ConditionsJson = conditionsJson;
                routingRule.TargetPartnerId = partnerId;
                routingRule.TargetConnectorId = connectorId;
                routingRule.Priority = seed.Priority;
                routingRule.IsActive = true;
                routingRule.UpdatedAt = now;
                routingRule.UpdatedBy = userId;
            }

            partnerIdsByCountry[seed.CountryCode] = partnerId;
            connectorIdsByCountry[seed.CountryCode] = connectorId;
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        _results[DemoSeedResultKeys.PartnerIdsByCountry] = partnerIdsByCountry;
        _results[DemoSeedResultKeys.ConnectorIdsByCountry] = connectorIdsByCountry;

        return new[] { "Seeded cross-border partner network and routing rules" };
    }

    // ── Phase: CrossBorderCatalog ────────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedCrossBorderCatalogAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;
        var operations = new List<string>();

        var partnerIdsByCountry = (Dictionary<string, Guid>)_results[DemoSeedResultKeys.PartnerIdsByCountry];

        if (!partnerIdsByCountry.TryGetValue("GH", out var ghPartnerId))
            throw new InvalidOperationException("Cross-border partner for GH is required.");
        if (!partnerIdsByCountry.TryGetValue("NG", out var ngPartnerId))
            throw new InvalidOperationException("Cross-border partner for NG is required.");
        if (!partnerIdsByCountry.TryGetValue("KE", out var kePartnerId))
            throw new InvalidOperationException("Cross-border partner for KE is required.");
        if (!partnerIdsByCountry.TryGetValue("ZA", out var zaPartnerId))
            throw new InvalidOperationException("Cross-border partner for ZA is required.");

        var ghCategoryId = await UpsertCategoryAsync(tenantId, UtilitiesCategoryId, "GH", "Utilities", "Electricity and water billers", 1, now, userId, operations, cancellationToken);
        var ngCategoryId = await UpsertCategoryAsync(tenantId, NigeriaUtilitiesCategoryId, "NG", "Utilities", "Electricity and water billers in Nigeria", 1, now, userId, operations, cancellationToken);
        var keCategoryId = await UpsertCategoryAsync(tenantId, KenyaUtilitiesCategoryId, "KE", "Utilities", "Electricity billers in Kenya", 1, now, userId, operations, cancellationToken);
        var zaCategoryId = await UpsertCategoryAsync(tenantId, SouthAfricaUtilitiesCategoryId, "ZA", "Utilities", "Electricity billers in South Africa", 1, now, userId, operations, cancellationToken);

        var ecgBillerId = await UpsertBillerAsync(tenantId, ghCategoryId, EcgBillerId, "ECG Power", "Ghana's electricity provider.", now, userId, operations, cancellationToken, ghPartnerId, "GH");
        var ghWaterBillerId = await UpsertBillerAsync(tenantId, ghCategoryId, GhanaWaterBillerId, "Ghana Water", "National water utility.", now, userId, operations, cancellationToken, ghPartnerId, "GH");
        var ikejaBillerId = await UpsertBillerAsync(tenantId, ngCategoryId, IkejaElectricBillerId, "Ikeja Electric", "Prepaid electricity in Lagos.", now, userId, operations, cancellationToken, ngPartnerId, "NG");
        var lagosWaterBillerId = await UpsertBillerAsync(tenantId, ngCategoryId, LagosWaterBillerId, "Lagos Water Board", "Postpaid water services for Lagos residents.", now, userId, operations, cancellationToken, ngPartnerId, "NG");
        var kenyaPowerBillerId = await UpsertBillerAsync(tenantId, keCategoryId, KenyaPowerBillerId, "Kenya Power", "National electricity distribution utility.", now, userId, operations, cancellationToken, kePartnerId, "KE");
        var cityPowerBillerId = await UpsertBillerAsync(tenantId, zaCategoryId, CityPowerBillerId, "City Power Johannesburg", "Municipal electricity provider.", now, userId, operations, cancellationToken, zaPartnerId, "ZA");

        var ecgServiceId = await UpsertServiceAsync(tenantId, ecgBillerId, EcgPrepaidServiceId, "BILLPAY.ELECTRICITY.PREPAID", "ECG Prepaid Electricity", "Prepaid", "GHS", 5, 500, true, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{EcgBillerId}/services/{EcgPrepaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var ghWaterServiceId = await UpsertServiceAsync(tenantId, ghWaterBillerId, GhanaWaterServiceId, "BILLPAY.WATER.POSTPAID", "Ghana Water Postpaid", "Postpaid", "GHS", 10, 1000, false, false,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 6, 20, null, "Enter account number", null) }),
            null, operations, cancellationToken);

        var ikejaServiceId = await UpsertServiceAsync(tenantId, ikejaBillerId, IkejaPrepaidServiceId, "BILLPAY.ELECTRICITY.PREPAID.NG.IKEJA", "Ikeja Prepaid Electricity", "Prepaid", "NGN", 500, 250000, true, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{IkejaElectricBillerId}/services/{IkejaPrepaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var ikejaPostpaidServiceId = await UpsertServiceAsync(tenantId, ikejaBillerId, IkejaPostpaidServiceId, "BILLPAY.ELECTRICITY.POSTPAID.NG.IKEJA", "Ikeja Postpaid Electricity", "Postpaid", "NGN", 1000, 400000, false, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{IkejaElectricBillerId}/services/{IkejaPostpaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var lagosWaterServiceId = await UpsertServiceAsync(tenantId, lagosWaterBillerId, LagosWaterServiceId, "BILLPAY.WATER.POSTPAID.NG.LAGOS", "Lagos Water Postpaid", "Postpaid", "NGN", 1000, 150000, false, false,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null) }),
            null, operations, cancellationToken);

        var lagosWaterPrepaidServiceId = await UpsertServiceAsync(tenantId, lagosWaterBillerId, LagosWaterPrepaidServiceId, "BILLPAY.WATER.PREPAID.NG.LAGOS", "Lagos Water Prepaid", "Prepaid", "NGN", 500, 150000, true, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{LagosWaterBillerId}/services/{LagosWaterPrepaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var kenyaPowerServiceId = await UpsertServiceAsync(tenantId, kenyaPowerBillerId, KenyaPowerServiceId, "BILLPAY.ELECTRICITY.PREPAID.KE.KPLC", "Kenya Power Prepaid", "Prepaid", "KES", 100, 150000, true, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("nationalId", "National ID", "text", true, 6, 12, null, "Enter national ID", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{KenyaPowerBillerId}/services/{KenyaPowerServiceId}/validate", "precheck")), operations, cancellationToken);

        var kenyaPowerPostpaidServiceId = await UpsertServiceAsync(tenantId, kenyaPowerBillerId, KenyaPowerPostpaidServiceId, "BILLPAY.ELECTRICITY.POSTPAID.KE.KPLC", "Kenya Power Postpaid", "Postpaid", "KES", 250, 200000, false, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null), new CatalogServiceField("nationalId", "National ID", "text", true, 6, 12, null, "Enter national ID", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{KenyaPowerBillerId}/services/{KenyaPowerPostpaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var cityPowerServiceId = await UpsertServiceAsync(tenantId, cityPowerBillerId, CityPowerServiceId, "BILLPAY.ELECTRICITY.PREPAID.ZA.CPJ", "City Power Prepaid", "Prepaid", "ZAR", 10, 25000, true, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("surname", "Surname", "text", true, 2, 80, null, "Enter surname", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{CityPowerBillerId}/services/{CityPowerServiceId}/validate", "precheck")), operations, cancellationToken);

        var cityPowerPostpaidServiceId = await UpsertServiceAsync(tenantId, cityPowerBillerId, CityPowerPostpaidServiceId, "BILLPAY.ELECTRICITY.POSTPAID.ZA.CPJ", "City Power Postpaid", "Postpaid", "ZAR", 50, 50000, false, true,
            BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null), new CatalogServiceField("surname", "Surname", "text", true, 2, 80, null, "Enter surname", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{CityPowerBillerId}/services/{CityPowerPostpaidServiceId}/validate", "precheck")), operations, cancellationToken);

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Extended catalog for NG, GH, KE, and ZA bill collection corridors");

        _results[DemoSeedResultKeys.CrossBorderCategoryIds] = new[] { ghCategoryId, ngCategoryId, keCategoryId, zaCategoryId };
        _results[DemoSeedResultKeys.CrossBorderBillerIds] = new[] { ecgBillerId, ghWaterBillerId, ikejaBillerId, lagosWaterBillerId, kenyaPowerBillerId, cityPowerBillerId };
        _results[DemoSeedResultKeys.CrossBorderServiceIds] = new[]
        {
            ecgServiceId, ghWaterServiceId, ikejaServiceId, ikejaPostpaidServiceId,
            lagosWaterServiceId, lagosWaterPrepaidServiceId, kenyaPowerServiceId,
            kenyaPowerPostpaidServiceId, cityPowerServiceId, cityPowerPostpaidServiceId
        };

        return operations;
    }

    // ── Phase: Households ────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedHouseholdsAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;

        var households = new List<(Guid HouseholdId, string Name, Guid MemberId, string Role, string PermissionsJson)>
        {
            (FamilyHouseholdId, "Mensah Household", FamilyHouseholdMemberId, "Owner", JsonSerializer.Serialize(new[] { "Bills.Manage", "Goals.Manage" })),
            (ProfessionalsHouseholdId, "Cross-Border Professionals", ProfessionalsHouseholdMemberId, "Member", JsonSerializer.Serialize(new[] { "Bills.View", "Budget.View" }))
        };

        var householdIds = new List<Guid>();
        var householdMemberIds = new List<Guid>();

        foreach (var seed in households)
        {
            var household = await _financeDbContext.Households
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Name == seed.Name, cancellationToken);

            if (household == null)
            {
                household = new Household
                {
                    Id = seed.HouseholdId,
                    TenantId = tenantId,
                    Name = seed.Name,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _financeDbContext.Households.Add(household);
            }
            else
            {
                household.Name = seed.Name;
                household.UpdatedAt = now;
                household.UpdatedBy = userId;
            }

            householdIds.Add(household.Id);

            if (!userId.HasValue)
            {
                continue;
            }

            var existingMember = await _financeDbContext.HouseholdMembers
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.HouseholdId == household.Id && item.UserId == userId.Value, cancellationToken);

            if (existingMember == null)
            {
                existingMember = new HouseholdMember
                {
                    Id = seed.MemberId,
                    TenantId = tenantId,
                    HouseholdId = household.Id,
                    UserId = userId.Value,
                    Role = seed.Role,
                    PermissionsJson = seed.PermissionsJson,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                _financeDbContext.HouseholdMembers.Add(existingMember);
            }
            else
            {
                existingMember.TenantId = tenantId;
                existingMember.Role = seed.Role;
                existingMember.PermissionsJson = seed.PermissionsJson;
                existingMember.UpdatedAt = now;
                existingMember.UpdatedBy = userId;
            }

            householdMemberIds.Add(existingMember.Id);
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        _results[DemoSeedResultKeys.HouseholdIds] = householdIds;
        _results[DemoSeedResultKeys.HouseholdMemberIds] = householdMemberIds;

        return new[] { "Seeded household groups for personal finance demos" };
    }

    // ── Phase: CrossBorderPricing ────────────────────────────────────

    private async Task<IReadOnlyList<string>> SeedCrossBorderPricingAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;

        var fxQuoteIds = new List<Guid>
        {
            await UpsertFxQuoteAsync(tenantId, DemoFxQuoteId, "NGN", "GHS", 0.0076m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-GH" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, NgnKesFxQuoteId, "NGN", "KES", 0.083m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-KE" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, NgnZarFxQuoteId, "NGN", "ZAR", 0.011m, "DemoRate", JsonSerializer.Serialize(new { corridor = "NG-ZA" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, UsdGhsFxQuoteId, "USD", "GHS", 12.85m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-GH" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, UsdKesFxQuoteId, "USD", "KES", 150.40m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-KE" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, UsdZarFxQuoteId, "USD", "ZAR", 18.60m, "DemoRate", JsonSerializer.Serialize(new { corridor = "USD-ZA" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpNgnFxQuoteId, "GBP", "NGN", 1985.25m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-NG" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpGhsFxQuoteId, "GBP", "GHS", 16.78m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-GH" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpKesFxQuoteId, "GBP", "KES", 168.45m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-KE" }), now, userId, cancellationToken),
            await UpsertFxQuoteAsync(tenantId, GbpZarFxQuoteId, "GBP", "ZAR", 24.31m, "DemoRate", JsonSerializer.Serialize(new { corridor = "UK-ZA" }), now, userId, cancellationToken)
        };

        var breakdown = new List<FeeBreakdownDefinition>
        {
            new("SERVICE_FEE", "Service fee", "Fixed"),
            new("FX_MARKUP", "FX markup", "FxMarkup")
        };

        var feePolicyIds = new List<Guid>
        {
            await UpsertFeePolicyAsync(tenantId, CrossBorderBand1FeePolicyId, "CrossBorder-NG-GH-Band-001-100", 40m, 0.010m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 0m, 100m, 20m, 180m, 120, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await UpsertFeePolicyAsync(tenantId, CrossBorderBand2FeePolicyId, "CrossBorder-NG-GH-Band-100-1000", 85m, 0.012m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 100.01m, 1000m, 35m, 400m, 150, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await UpsertFeePolicyAsync(tenantId, CrossBorderBand3FeePolicyId, "CrossBorder-NG-GH-Band-1000-Plus", 150m, 0.015m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID", "NG", "GH", "NGN", "GHS", "Retail", 1000.01m, null, 60m, 1250m, 180, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await UpsertFeePolicyAsync(tenantId, CrossBorderKesFeePolicyId, "CrossBorder-NG-KE-Default", 75m, 0.013m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID.KE.KPLC", "NG", "KE", "NGN", "KES", "Retail", null, null, 30m, 600m, 140, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken),
            await UpsertFeePolicyAsync(tenantId, CrossBorderZarFeePolicyId, "CrossBorder-NG-ZA-Default", 90m, 0.014m,
                new FeePolicyConditions("BILLPAY.ELECTRICITY.PREPAID.ZA.CPJ", "NG", "ZA", "NGN", "ZAR", "Retail", null, null, 40m, 750m, 150, "DemoRate", "AwayFromZero", breakdown), now, userId, cancellationToken)
        };

        var limitsPolicyIds = new List<Guid>
        {
            await UpsertLimitsPolicyAsync(tenantId, DemoLimitsPolicyId, "NGN", 5000000m, "Daily", now, userId, cancellationToken),
            await UpsertLimitsPolicyAsync(tenantId, KenyaLimitsPolicyId, "KES", 300000m, "Daily", now, userId, cancellationToken),
            await UpsertLimitsPolicyAsync(tenantId, SouthAfricaLimitsPolicyId, "ZAR", 120000m, "Daily", now, userId, cancellationToken)
        };

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        _results[DemoSeedResultKeys.CrossBorderFxQuoteIds] = fxQuoteIds;
        _results[DemoSeedResultKeys.CrossBorderFeePolicyIds] = feePolicyIds;
        _results[DemoSeedResultKeys.CrossBorderLimitsPolicyIds] = limitsPolicyIds;

        return new[] { "Seeded UK-to-Africa FX quotes and tiered cross-border charging policies" };
    }

    // ── Shared private helpers ───────────────────────────────────────

    private async Task EnsurePartnerPrefundAccountAsync(
        Guid tenantId,
        Guid partnerId,
        string partnerName,
        string currencyCode,
        decimal openingBalance,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);

        var fundingAccount = await _financeDbContext.PartnerFundingAccounts
            .FirstOrDefaultAsync(account =>
                account.TenantId == tenantId &&
                account.PartnerId == partnerId &&
                account.Currency == normalizedCurrency &&
                account.AccountRole == PrefundAccountRole,
                cancellationToken);

        var accountCode = BuildPartnerPrefundAccountCode(partnerId, normalizedCurrency);
        var ledgerAccount = await _financeDbContext.LedgerAccounts
            .FirstOrDefaultAsync(account => account.TenantId == tenantId && account.Code == accountCode, cancellationToken);

        if (ledgerAccount == null)
        {
            ledgerAccount = new LedgerAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerId = ledgerId,
                AccountType = "Asset",
                Name = $"Due From Partner {partnerName} ({normalizedCurrency})",
                Code = accountCode,
                DimensionsJson = JsonSerializer.Serialize(new
                {
                    partnerId,
                    currency = normalizedCurrency,
                    accountRole = PrefundAccountRole
                }),
                CreatedAt = now,
                CreatedBy = userId
            };

            _financeDbContext.LedgerAccounts.Add(ledgerAccount);
        }

        if (fundingAccount == null)
        {
            fundingAccount = new PartnerFundingAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartnerId = partnerId,
                LedgerAccountId = ledgerAccount.Id,
                Currency = normalizedCurrency,
                AccountRole = PrefundAccountRole,
                Status = "Active",
                CreatedAt = now,
                CreatedBy = userId
            };

            _financeDbContext.PartnerFundingAccounts.Add(fundingAccount);
        }
        else
        {
            fundingAccount.LedgerAccountId = ledgerAccount.Id;
            fundingAccount.Currency = normalizedCurrency;
            fundingAccount.Status = "Active";
            fundingAccount.UpdatedAt = now;
            fundingAccount.UpdatedBy = userId;
        }

        await EnsurePartnerPrefundOpeningEntryAsync(
            tenantId,
            ledgerId,
            fundingAccount,
            openingBalance,
            now,
            userId,
            cancellationToken);
    }

    private async Task EnsurePartnerPrefundOpeningEntryAsync(
        Guid tenantId,
        Guid ledgerId,
        PartnerFundingAccount fundingAccount,
        decimal openingBalance,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (openingBalance <= 0)
        {
            return;
        }

        var hasSeedEntry = await _financeDbContext.JournalEntries
            .AsNoTracking()
            .AnyAsync(entry =>
                entry.TenantId == tenantId &&
                entry.SourceType == PartnerPrefundSeedSourceType &&
                entry.SourceId == fundingAccount.Id,
                cancellationToken);

        if (hasSeedEntry)
        {
            return;
        }

        var cashAccountId = await ResolveCashLedgerAccountIdAsync(tenantId, cancellationToken);
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledgerId,
            Timestamp = now,
            SourceType = PartnerPrefundSeedSourceType,
            SourceId = fundingAccount.Id,
            Status = "Posted",
            CreatedAt = now,
            CreatedBy = userId,
            Lines = new List<JournalEntryLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerAccountId = fundingAccount.LedgerAccountId,
                    Direction = "Debit",
                    Amount = openingBalance,
                    Currency = fundingAccount.Currency,
                    Narration = "Seed prefund opening balance",
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId = fundingAccount.PartnerId,
                        fundingAccountId = fundingAccount.Id,
                        accountRole = fundingAccount.AccountRole
                    }),
                    CreatedAt = now,
                    CreatedBy = userId
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    LedgerAccountId = cashAccountId,
                    Direction = "Credit",
                    Amount = openingBalance,
                    Currency = fundingAccount.Currency,
                    Narration = "Seed prefund opening balance",
                    DimensionsJson = JsonSerializer.Serialize(new
                    {
                        partnerId = fundingAccount.PartnerId,
                        fundingAccountId = fundingAccount.Id,
                        accountRole = fundingAccount.AccountRole
                    }),
                    CreatedAt = now,
                    CreatedBy = userId
                }
            }
        };

        _financeDbContext.JournalEntries.Add(entry);
    }

    private async Task<Guid> ResolveCashLedgerAccountIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cashAccountId = await _financeDbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Code == "1000")
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (cashAccountId.HasValue)
        {
            return cashAccountId.Value;
        }

        cashAccountId = await _financeDbContext.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Name == "Cash")
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!cashAccountId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a cash ledger account for prefund seeding.");
        }

        return cashAccountId.Value;
    }

    private async Task<Guid> GetTenantLedgerIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var ledgerId = await _financeDbContext.Ledgers
            .AsNoTracking()
            .Where(ledger => ledger.TenantId == tenantId)
            .Select(ledger => (Guid?)ledger.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ledgerId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a ledger.");
        }

        return ledgerId.Value;
    }

    private static string BuildPartnerPrefundAccountCode(Guid partnerId, string currencyCode)
    {
        var partnerCode = partnerId.ToString("N")[..12].ToUpperInvariant();
        return $"1300-{partnerCode}-{currencyCode}";
    }

    private async Task<Guid> UpsertBillerAsync(
        Guid tenantId,
        Guid categoryId,
        Guid billerId,
        string name,
        string description,
        DateTime now,
        Guid? userId,
        List<string> operations,
        CancellationToken cancellationToken,
        Guid correspondentPartnerId,
        string countryCode = "GH")
    {
        var biller = await _financeDbContext.CatalogBillers
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == countryCode
                                         && item.Name == name,
                cancellationToken);

        if (biller == null)
        {
            biller = new CatalogBiller
            {
                Id = billerId,
                TenantId = tenantId,
                CategoryId = categoryId,
                CountryCode = countryCode,
                Name = name,
                Description = description,
                CorrespondentPartnerId = correspondentPartnerId,
                SupportEmail = "support@aonik.demo",
                SupportPhone = "+233-000-0000",
                IsActive = true,
                IsFeatured = true,
                SortOrder = 1,
                CreatedAt = now,
                CreatedBy = userId
            };
            _financeDbContext.CatalogBillers.Add(biller);
            operations.Add($"Catalog biller seeded: {name}");
        }
        else
        {
            biller.CategoryId = categoryId;
            biller.Name = name;
            biller.Description = description;
            biller.CountryCode = countryCode;
            biller.CorrespondentPartnerId = correspondentPartnerId;
            biller.IsActive = true;
            biller.UpdatedAt = now;
            biller.UpdatedBy = userId;
        }

        return biller.Id;
    }

    private async Task<Guid> UpsertServiceAsync(
        Guid tenantId,
        Guid billerId,
        Guid serviceId,
        string serviceCode,
        string name,
        string type,
        string currency,
        decimal minAmount,
        decimal maxAmount,
        bool supportsPartial,
        bool requiresValidation,
        string fieldsJson,
        string? validationJson,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var service = await _financeDbContext.CatalogBillerServices
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.ServiceCode == serviceCode,
                cancellationToken);

        var now = DateTime.UtcNow;

        if (service == null)
        {
            service = new CatalogBillerService
            {
                Id = serviceId,
                TenantId = tenantId,
                BillerId = billerId,
                ServiceCode = serviceCode,
                Name = name,
                Type = type,
                Currency = currency,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                SupportsPartialPayment = supportsPartial,
                RequiresValidation = requiresValidation,
                IsActive = true,
                FieldsJson = fieldsJson,
                ValidationJson = validationJson,
                SortOrder = 1,
                CreatedAt = now
            };
            _financeDbContext.CatalogBillerServices.Add(service);
            operations.Add($"Catalog service seeded: {name}");
        }
        else
        {
            service.BillerId = billerId;
            service.ServiceCode = serviceCode;
            service.Name = name;
            service.Type = type;
            service.Currency = currency;
            service.MinAmount = minAmount;
            service.MaxAmount = maxAmount;
            service.SupportsPartialPayment = supportsPartial;
            service.RequiresValidation = requiresValidation;
            service.IsActive = true;
            service.FieldsJson = fieldsJson;
            service.ValidationJson = validationJson;
            service.UpdatedAt = now;
        }

        return service.Id;
    }

    private async Task<Guid> UpsertCategoryAsync(
        Guid tenantId,
        Guid categoryId,
        string countryCode,
        string name,
        string description,
        int sortOrder,
        DateTime now,
        Guid? userId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var category = await _financeDbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.CountryCode == countryCode
                                         && item.Name == name,
                cancellationToken);

        if (category == null)
        {
            category = new CatalogBillerCategory
            {
                Id = categoryId,
                TenantId = tenantId,
                CountryCode = countryCode,
                Name = name,
                Description = description,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = userId
            };
            _financeDbContext.CatalogBillerCategories.Add(category);
            operations.Add($"Catalog category seeded: {countryCode} {name}");
        }
        else
        {
            category.Name = name;
            category.Description = description;
            category.SortOrder = sortOrder;
            category.IsActive = true;
            category.UpdatedAt = now;
            category.UpdatedBy = userId;
        }

        return category.Id;
    }

    private static string BuildServiceFieldsJson(IEnumerable<CatalogServiceField> fields)
    {
        return JsonSerializer.Serialize(fields);
    }

    private async Task<Guid> UpsertFxQuoteAsync(
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
        var fxQuote = await _financeDbContext.FxQuotes
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
            _financeDbContext.FxQuotes.Add(fxQuote);
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

    private async Task<Guid> UpsertFeePolicyAsync(
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
        var feePolicy = await _financeDbContext.FeePolicies
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
            _financeDbContext.FeePolicies.Add(feePolicy);
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

    private async Task<Guid> UpsertLimitsPolicyAsync(
        Guid tenantId,
        Guid limitsPolicyId,
        string currency,
        decimal maxAmount,
        string period,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var limitsPolicy = await _financeDbContext.LimitsPolicies
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
            _financeDbContext.LimitsPolicies.Add(limitsPolicy);
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

    // ── Phase: Activity (Orders) ─────────────────────────────────────
    //
    // Seeds ~10 orders across the seeded personas so the /orders list isn't
    // empty after a fresh demo install. Mix of bill-payments and money-
    // transfers, mix of statuses (Pending / Transmitted / Complete / Failed),
    // spread across the last 14 days. Re-seeding is idempotent — orders
    // are upserted by deterministic Guid.

    private static readonly Guid OrderKwameEcg          = SeedIds.OrderActivity.OrderKwameEcg;
    private static readonly Guid OrderKwameWater        = SeedIds.OrderActivity.OrderKwameWater;
    private static readonly Guid OrderTundeIkeja        = SeedIds.OrderActivity.OrderTundeIkeja;
    private static readonly Guid OrderTundeLagosWater   = SeedIds.OrderActivity.OrderTundeLagosWater;
    private static readonly Guid OrderAcmePayoutNg      = SeedIds.OrderActivity.OrderAcmePayoutNg;
    private static readonly Guid OrderAdwoaWaterFailed  = SeedIds.OrderActivity.OrderAdwoaWaterFailed;
    private static readonly Guid OrderOliviaToNaledi    = SeedIds.OrderActivity.OrderOliviaToNaledi;
    private static readonly Guid OrderLiamToKwame       = SeedIds.OrderActivity.OrderLiamToKwame;
    private static readonly Guid OrderKofiAmaTransfer   = SeedIds.OrderActivity.OrderKofiAmaTransfer;
    private static readonly Guid OrderPeterKenyaPower   = SeedIds.OrderActivity.OrderPeterKenyaPower;

    private static readonly Guid DemoPayerPartyId    = SeedIds.PartyReferences.DemoPayerPartyId;
    private static readonly Guid DemoReceiverPartyId = SeedIds.PartyReferences.DemoReceiverPartyId;
    private static readonly Guid TundePartyIdRef     = SeedIds.PartyReferences.TundePartyId;
    private static readonly Guid AdwoaPartyIdRef     = SeedIds.PartyReferences.AdwoaPartyId;
    private static readonly Guid PeterPartyIdRef     = SeedIds.PartyReferences.PeterPartyId;
    private static readonly Guid NalediPartyIdRef    = SeedIds.PartyReferences.NalediPartyId;
    private static readonly Guid KofiPartyIdRef      = SeedIds.PartyReferences.KofiPartyId;
    private static readonly Guid AcmeImportsPartyIdRef = SeedIds.PartyReferences.AcmeImportsPartyId;
    private static readonly Guid OliviaPartyIdRef    = SeedIds.PartyReferences.OliviaPartyId;
    private static readonly Guid LiamPartyIdRef      = SeedIds.PartyReferences.LiamPartyId;

    private async Task<IReadOnlyList<string>> SeedOrderActivityAsync(
        DemoSeedContext context,
        CancellationToken cancellationToken)
    {
        var operations = new List<string>();
        var now = context.Now;
        var userId = context.UserId;
        var orderIds = new List<Guid>();

        var seeds = BuildOrderSeeds(context.SeedType, now);

        foreach (var seed in seeds)
        {
            var existing = await _financeDbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == seed.OrderId && o.TenantId == context.TenantId,
                    cancellationToken);

            if (existing is null)
            {
                existing = new Aonik.Finance.Entities.Orders.Order
                {
                    Id = seed.OrderId,
                    TenantId = context.TenantId,
                    OrderType = seed.OrderType,
                    PayerPartyId = seed.PayerPartyId,
                    PurposeCode = seed.PurposeCode,
                    OriginCountry = seed.OriginCountry,
                    DestinationCountry = seed.DestinationCountry,
                    AmountIn = seed.AmountIn,
                    CurrencyIn = seed.CurrencyIn,
                    AmountOut = seed.AmountOut,
                    CurrencyOut = seed.CurrencyOut,
                    FeesJson = "[]",
                    Status = seed.Status,
                    ProvenanceJson = "{\"source\":\"demo-seed\"}",
                    CreatedAt = seed.CreatedAt,
                    CreatedBy = userId,
                };
                _financeDbContext.Orders.Add(existing);
                operations.Add($"Seeded order {seed.OrderId:D}");
            }
            else
            {
                existing.OrderType = seed.OrderType;
                existing.Status = seed.Status;
                existing.AmountIn = seed.AmountIn;
                existing.CurrencyIn = seed.CurrencyIn;
                existing.AmountOut = seed.AmountOut;
                existing.CurrencyOut = seed.CurrencyOut;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
            }

            orderIds.Add(seed.OrderId);

            // Replace items + party roles each run — hard-delete via
            // ExecuteDeleteAsync so the audit hook doesn't soft-delete
            // them and leave ghost rows on the next re-seed.
            await _financeDbContext.OrderItems
                .IgnoreQueryFilters()
                .Where(i => i.OrderId == seed.OrderId)
                .ExecuteDeleteAsync(cancellationToken);

            for (var idx = 0; idx < seed.Items.Count; idx++)
            {
                var item = seed.Items[idx];
                _financeDbContext.OrderItems.Add(new Aonik.Finance.Entities.Orders.OrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    OrderId = seed.OrderId,
                    ItemType = item.ItemType,
                    ItemIndex = idx,
                    DetailsJson = item.DetailsJson,
                    Status = seed.Status,
                    ReceiverPartyId = item.ReceiverPartyId,
                    AmountIn = item.AmountIn,
                    CurrencyIn = item.CurrencyIn,
                    AmountOut = item.AmountOut,
                    CurrencyOut = item.CurrencyOut,
                    FeesTotal = item.FeesTotal,
                    CreatedAt = seed.CreatedAt,
                    CreatedBy = userId,
                });
            }

            await _financeDbContext.OrderPartyRoles
                .IgnoreQueryFilters()
                .Where(r => r.OrderId == seed.OrderId)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var role in seed.PartyRoles)
            {
                _financeDbContext.OrderPartyRoles.Add(new Aonik.Finance.Entities.Orders.OrderPartyRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    OrderId = seed.OrderId,
                    PartyId = role.PartyId,
                    Role = role.Role,
                    CreatedAt = seed.CreatedAt,
                    CreatedBy = userId,
                });
            }
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        _results[DemoSeedResultKeys.OrderIds] = orderIds.ToArray();
        return operations;
    }

    private static IReadOnlyList<DemoOrderSeed> BuildOrderSeeds(string seedType, DateTime now)
    {
        // Spread orders across the last 14 days. Mix BillPayment / BankTransfer
        // and statuses to populate the registry filters with actual variety.
        // Bill collection is the default seed; cross-border adds three
        // UK→Africa flows that exercise the corridor pages too.
        var seeds = new List<DemoOrderSeed>
        {
            BillPay(OrderKwameEcg,         DemoPayerPartyId,  "GH", 250m,  "GHS", "ECG",          OrderStatuses.Pending,     now.AddDays(-1)),
            BillPay(OrderKwameWater,       DemoPayerPartyId,  "GH",  90m,  "GHS", "GhanaWater",   OrderStatuses.Complete,    now.AddDays(-3)),
            BillPay(OrderTundeIkeja,       TundePartyIdRef,   "NG", 8500m, "NGN", "IkejaElectric", OrderStatuses.Pending,    now.AddHours(-6)),
            BillPay(OrderTundeLagosWater,  TundePartyIdRef,   "NG", 4200m, "NGN", "LagosWater",   OrderStatuses.Transmitted, now.AddDays(-2)),
            BillPay(OrderAdwoaWaterFailed, AdwoaPartyIdRef,   "GH",  60m,  "GHS", "GhanaWater",   OrderStatuses.Failed,      now.AddDays(-9),
                detailsExtra: ",\"failureReason\":\"insufficient_funds\""),
            Transfer(OrderAcmePayoutNg, AcmeImportsPartyIdRef, PeterPartyIdRef, "GB", "NG", 2500m, "GBP", 4_902_500m, "NGN", 8.50m,
                "supplier_payment", OrderStatuses.Complete, now.AddDays(-5)),
            Transfer(OrderKofiAmaTransfer, KofiPartyIdRef, DemoReceiverPartyId, "GH", "GH", 200m, "GHS", 200m, "GHS", 1.50m,
                "family_support", OrderStatuses.Complete, now.AddDays(-7)),
        };

        if (string.Equals(seedType, "CrossBorderPayments", StringComparison.OrdinalIgnoreCase))
        {
            seeds.Add(Transfer(OrderOliviaToNaledi, OliviaPartyIdRef, NalediPartyIdRef, "GB", "ZA", 1500m, "GBP", 35_550m, "ZAR", 6.50m,
                "remittance", OrderStatuses.Complete, now.AddDays(-4)));
            seeds.Add(Transfer(OrderLiamToKwame, LiamPartyIdRef, DemoPayerPartyId, "GB", "GH", 750m, "GBP", 11_400m, "GHS", 4.50m,
                "remittance", OrderStatuses.Pending, now.AddHours(-12)));
            seeds.Add(BillPay(OrderPeterKenyaPower, PeterPartyIdRef, "KE", 1200m, "KES", "KenyaPower", OrderStatuses.Complete, now.AddDays(-8)));
        }

        return seeds;
    }

    private static DemoOrderSeed BillPay(
        Guid orderId,
        Guid payerPartyId,
        string country,
        decimal amount,
        string currency,
        string biller,
        string status,
        DateTime createdAt,
        string detailsExtra = "")
    {
        var details = $"{{\"biller\":\"{biller}\"{detailsExtra}}}";
        return new DemoOrderSeed(
            OrderId: orderId,
            OrderType: "BillPayment",
            PayerPartyId: payerPartyId,
            PurposeCode: "BillPayment",
            OriginCountry: country,
            DestinationCountry: country,
            AmountIn: amount,
            CurrencyIn: currency,
            AmountOut: amount,
            CurrencyOut: currency,
            Status: status,
            CreatedAt: createdAt,
            Items: new[] { new DemoOrderItemSeed("BillPayment", payerPartyId, amount, currency, amount, currency, 0m, details) },
            PartyRoles: new[]
            {
                new DemoOrderRoleSeed(payerPartyId, "Payer"),
                new DemoOrderRoleSeed(payerPartyId, "Payee"),
            });
    }

    private static DemoOrderSeed Transfer(
        Guid orderId,
        Guid payerPartyId,
        Guid receiverPartyId,
        string originCountry,
        string destinationCountry,
        decimal amountIn,
        string currencyIn,
        decimal amountOut,
        string currencyOut,
        decimal feesTotal,
        string purpose,
        string status,
        DateTime createdAt)
    {
        var details = $"{{\"purpose\":\"{purpose}\",\"corridor\":\"{originCountry}-{destinationCountry}\"}}";
        return new DemoOrderSeed(
            OrderId: orderId,
            OrderType: "BankTransfer",
            PayerPartyId: payerPartyId,
            PurposeCode: purpose,
            OriginCountry: originCountry,
            DestinationCountry: destinationCountry,
            AmountIn: amountIn,
            CurrencyIn: currencyIn,
            AmountOut: amountOut,
            CurrencyOut: currencyOut,
            Status: status,
            CreatedAt: createdAt,
            Items: new[] { new DemoOrderItemSeed("BankTransfer", receiverPartyId, amountIn, currencyIn, amountOut, currencyOut, feesTotal, details) },
            PartyRoles: new[]
            {
                new DemoOrderRoleSeed(payerPartyId, "Payer"),
                new DemoOrderRoleSeed(receiverPartyId, "Receiver"),
            });
    }

    private sealed record DemoOrderSeed(
        Guid OrderId,
        string OrderType,
        Guid PayerPartyId,
        string PurposeCode,
        string OriginCountry,
        string DestinationCountry,
        decimal AmountIn,
        string CurrencyIn,
        decimal AmountOut,
        string CurrencyOut,
        string Status,
        DateTime CreatedAt,
        IReadOnlyList<DemoOrderItemSeed> Items,
        IReadOnlyList<DemoOrderRoleSeed> PartyRoles);

    private sealed record DemoOrderItemSeed(
        string ItemType,
        Guid ReceiverPartyId,
        decimal AmountIn,
        string CurrencyIn,
        decimal AmountOut,
        string CurrencyOut,
        decimal FeesTotal,
        string DetailsJson);

    private sealed record DemoOrderRoleSeed(Guid PartyId, string Role);
}
