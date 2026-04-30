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

    #region Static Guid constants

    // Catalog
    private static readonly Guid UtilitiesCategoryId = Guid.Parse("9de53a10-0f7c-4ce5-9ef4-6305656135e1");
    private static readonly Guid EcgBillerId = Guid.Parse("aa7d7c1c-4aab-4b51-8b0a-155d42c328f8");
    private static readonly Guid GhanaWaterBillerId = Guid.Parse("0f3b7b2a-c5c2-4d06-b8a2-6f3f28f0b2c5");
    private static readonly Guid EcgPrepaidServiceId = Guid.Parse("3c1f6a6a-73cf-4be0-a15d-2ed45e8d3577");
    private static readonly Guid GhanaWaterServiceId = Guid.Parse("c4a7f65d-2f7a-4b77-9a7c-5c9c9b8a7c91");
    private static readonly Guid EcgPostpaidServiceId = Guid.Parse("9e1a2ff2-7f48-45fd-9af7-3d2d9cf5241e");
    private static readonly Guid GhanaWaterPrepaidServiceId = Guid.Parse("8f80cb7d-fc6e-4b8f-8998-0f683ecf3f58");

    // Pricing
    private static readonly Guid DemoFxQuoteId = Guid.Parse("9a8d9f56-b91b-4d1a-8f7a-2e12a54e50e2");
    private static readonly Guid DemoFeePolicyId = Guid.Parse("7b6b3b5d-91b9-4d25-8f2c-ead45812c1a1");
    private static readonly Guid DemoLimitsPolicyId = Guid.Parse("5a8dd1d8-1f47-41f5-9e8d-1ef1e7c7880a");

    // Cross-border categories
    private static readonly Guid NigeriaUtilitiesCategoryId = Guid.Parse("6d67a8f3-9242-4d42-a7fc-a097b2f8f13a");
    private static readonly Guid KenyaUtilitiesCategoryId = Guid.Parse("9f2287f1-6c0e-4c53-bf67-a6fcbbdd4194");
    private static readonly Guid SouthAfricaUtilitiesCategoryId = Guid.Parse("dc9fd7e0-f74f-4181-b643-fdf678c113f6");

    // Cross-border billers
    private static readonly Guid IkejaElectricBillerId = Guid.Parse("eec98e01-8ab4-4f61-a4d4-c4409f1f596e");
    private static readonly Guid LagosWaterBillerId = Guid.Parse("d59fb89a-efcf-4069-a4f8-7ed1cf1b9fd9");
    private static readonly Guid KenyaPowerBillerId = Guid.Parse("f5f91117-a466-4f89-b7e4-ce1b6ace9f9a");
    private static readonly Guid CityPowerBillerId = Guid.Parse("3d6622ff-7661-4f43-bf6a-2a3f6ae97f8c");

    // Cross-border services
    private static readonly Guid IkejaPrepaidServiceId = Guid.Parse("60d7de6b-e579-412a-bbe6-f7fc6cad2b2d");
    private static readonly Guid IkejaPostpaidServiceId = Guid.Parse("a7d13065-8e2a-47c9-a84f-4f9725448e2b");
    private static readonly Guid LagosWaterServiceId = Guid.Parse("bc767227-e727-4370-b54b-a52cd57774e8");
    private static readonly Guid LagosWaterPrepaidServiceId = Guid.Parse("f6bbca26-05b4-47c3-afca-f3f7453c189f");
    private static readonly Guid KenyaPowerServiceId = Guid.Parse("61a14f31-37e8-4fc1-8f8a-22ca7ddd8efe");
    private static readonly Guid KenyaPowerPostpaidServiceId = Guid.Parse("46ddbdce-446e-4898-8a4b-b8a28f6999aa");
    private static readonly Guid CityPowerServiceId = Guid.Parse("5b997ce8-66dc-4fc2-9e1b-a7144a3294b6");
    private static readonly Guid CityPowerPostpaidServiceId = Guid.Parse("6c99f4f0-8d6b-4e5d-a6a5-65bdcb7e6f4f");

    // Partners
    private static readonly Guid NigeriaPartnerId = Guid.Parse("f8b8a6cb-7f85-45aa-84af-7ce4d17172af");
    private static readonly Guid GhanaPartnerId = Guid.Parse("5f8fa8a8-f16a-4256-b7ea-32a8322c2f8d");
    private static readonly Guid KenyaPartnerId = Guid.Parse("3da50f8d-5f9b-4c27-96f1-c7c603ec073d");
    private static readonly Guid SouthAfricaPartnerId = Guid.Parse("fca95d87-cf29-4e57-b931-f26f76f052da");

    // Branches
    private static readonly Guid NigeriaBranchId = Guid.Parse("9021f646-0525-43b8-bfb7-6fd7482c5f95");
    private static readonly Guid GhanaBranchId = Guid.Parse("4f26abca-97a9-4806-8274-243cc87ecf9a");
    private static readonly Guid KenyaBranchId = Guid.Parse("ce53f9dd-8d80-4f49-acaf-9fac89efb4ba");
    private static readonly Guid SouthAfricaBranchId = Guid.Parse("ecf19036-575c-4226-ae93-50e7f6708f18");

    // Connectors
    private static readonly Guid NigeriaConnectorId = Guid.Parse("6dbd8515-d115-42e3-a3b9-721e4f0ad08a");
    private static readonly Guid GhanaConnectorId = Guid.Parse("f4aa2e03-03b7-4af7-850d-56919d2f5c86");
    private static readonly Guid KenyaConnectorId = Guid.Parse("ab58f337-5f04-4e5e-bec8-7d713267464f");
    private static readonly Guid SouthAfricaConnectorId = Guid.Parse("ac085fd8-6afa-49ef-a2f1-c4915334ad1d");

    // Routing rules
    private static readonly Guid NigeriaRoutingRuleId = Guid.Parse("890d6a45-5558-46c7-bf6b-8df3a15ce7f9");
    private static readonly Guid GhanaRoutingRuleId = Guid.Parse("e771f089-3167-42c5-98cd-f85e947e5ddf");
    private static readonly Guid KenyaRoutingRuleId = Guid.Parse("0072252c-8f31-485e-a0ac-8ff8df5263d9");
    private static readonly Guid SouthAfricaRoutingRuleId = Guid.Parse("6e93d3eb-c8f6-4c5d-abd4-c7759a5048ab");

    // Households
    private static readonly Guid FamilyHouseholdId = Guid.Parse("96f58c5f-82f3-41b8-beb6-bf11fbcce5c2");
    private static readonly Guid ProfessionalsHouseholdId = Guid.Parse("89b29ec1-a771-4926-8897-ec7408ee8917");
    private static readonly Guid FamilyHouseholdMemberId = Guid.Parse("09f17349-c107-46b7-a3c9-c2bc42053a7e");
    private static readonly Guid ProfessionalsHouseholdMemberId = Guid.Parse("a8fdb3f6-8f7f-40f4-b55d-07d1997aebc7");

    // Cross-border FX quotes
    private static readonly Guid NgnKesFxQuoteId = Guid.Parse("32cc8c2b-76eb-4f97-b715-3bc8474f4ec7");
    private static readonly Guid NgnZarFxQuoteId = Guid.Parse("f4366a9d-550e-4cb2-af36-4134e6f62050");
    private static readonly Guid UsdGhsFxQuoteId = Guid.Parse("6d81b4d5-e8e0-46c0-ae17-a43eca0bfe61");
    private static readonly Guid UsdKesFxQuoteId = Guid.Parse("a30b90e1-784c-4fb9-ab2b-3941a93bc981");
    private static readonly Guid UsdZarFxQuoteId = Guid.Parse("a244a0b1-b6d4-4f95-827a-7001243b9d58");
    private static readonly Guid GbpNgnFxQuoteId = Guid.Parse("1496b1ee-6af8-4744-a740-239b4f8b8136");
    private static readonly Guid GbpGhsFxQuoteId = Guid.Parse("ca2992ea-9f4f-4347-98ea-65f8872ef8e4");
    private static readonly Guid GbpKesFxQuoteId = Guid.Parse("4874f7ab-1368-4414-b595-249143ca25da");
    private static readonly Guid GbpZarFxQuoteId = Guid.Parse("5cf25d4d-2834-4027-b84a-500f5f6e113f");

    // Cross-border fee policies
    private static readonly Guid CrossBorderBand1FeePolicyId = Guid.Parse("af7ae2fe-2f1d-4e8a-b6f1-2d3ce43af183");
    private static readonly Guid CrossBorderBand2FeePolicyId = Guid.Parse("6f87556a-8369-425d-a9ff-85082f7c3767");
    private static readonly Guid CrossBorderBand3FeePolicyId = Guid.Parse("45eb59de-9970-476f-8ee5-cc2f196f998e");
    private static readonly Guid CrossBorderKesFeePolicyId = Guid.Parse("8eebac13-e53c-4d2a-b00f-825026f0f3fb");
    private static readonly Guid CrossBorderZarFeePolicyId = Guid.Parse("06f9246e-ff04-4d5c-86f7-11ba24a57cc8");

    // Cross-border limits policies
    private static readonly Guid KenyaLimitsPolicyId = Guid.Parse("089177ce-b8ef-4f1a-8b95-ebcd6b6892e6");
    private static readonly Guid SouthAfricaLimitsPolicyId = Guid.Parse("bade6de0-6272-4e5d-9b3b-7f6f42fec4c3");

    // CatalogSeedService global biller categories (TenantId = Guid.Empty)
    private static readonly Guid GlobalUtilitiesCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GlobalTelecomCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GlobalInternetCategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid GlobalEducationCategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid GlobalGovernmentCategoryId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid GlobalCableCategoryId = Guid.Parse("66666666-6666-6666-6666-666666666666");

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
        var categories = new List<CatalogBillerCategory>
        {
            new()
            {
                Id = GlobalUtilitiesCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "GH",
                Name = "Utilities",
                Description = "Electricity and water",
                IconUrl = "https://cdn.aonik.io/catalog/icons/utilities.png",
                SortOrder = 1,
                IsActive = true
            },
            new()
            {
                Id = GlobalTelecomCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "GH",
                Name = "Telecom",
                Description = "Mobile and fixed line",
                IconUrl = "https://cdn.aonik.io/catalog/icons/telecom.png",
                SortOrder = 2,
                IsActive = true
            },
            new()
            {
                Id = GlobalInternetCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "NG",
                Name = "Internet",
                Description = "ISPs and broadband",
                IconUrl = "https://cdn.aonik.io/catalog/icons/internet.png",
                SortOrder = 3,
                IsActive = true
            },
            new()
            {
                Id = GlobalEducationCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "NG",
                Name = "Education",
                Description = "Tuition and school fees",
                IconUrl = "https://cdn.aonik.io/catalog/icons/education.png",
                SortOrder = 4,
                IsActive = true
            },
            new()
            {
                Id = GlobalGovernmentCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "KE",
                Name = "Government",
                Description = "Taxes and fees",
                IconUrl = "https://cdn.aonik.io/catalog/icons/government.png",
                SortOrder = 5,
                IsActive = true
            },
            new()
            {
                Id = GlobalCableCategoryId,
                TenantId = Guid.Empty,
                CountryCode = "KE",
                Name = "Cable",
                Description = "TV subscriptions",
                IconUrl = "https://cdn.aonik.io/catalog/icons/cable.png",
                SortOrder = 6,
                IsActive = true
            }
        };

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

    private static readonly Guid OrderKwameEcg          = Guid.Parse("aaaa0001-0000-0000-0000-000000000001");
    private static readonly Guid OrderKwameWater        = Guid.Parse("aaaa0001-0000-0000-0000-000000000002");
    private static readonly Guid OrderTundeIkeja        = Guid.Parse("aaaa0001-0000-0000-0000-000000000003");
    private static readonly Guid OrderTundeLagosWater   = Guid.Parse("aaaa0001-0000-0000-0000-000000000004");
    private static readonly Guid OrderAcmePayoutNg      = Guid.Parse("aaaa0001-0000-0000-0000-000000000005");
    private static readonly Guid OrderAdwoaWaterFailed  = Guid.Parse("aaaa0001-0000-0000-0000-000000000006");
    private static readonly Guid OrderOliviaToNaledi    = Guid.Parse("aaaa0001-0000-0000-0000-000000000007");
    private static readonly Guid OrderLiamToKwame       = Guid.Parse("aaaa0001-0000-0000-0000-000000000008");
    private static readonly Guid OrderKofiAmaTransfer   = Guid.Parse("aaaa0001-0000-0000-0000-000000000009");
    private static readonly Guid OrderPeterKenyaPower   = Guid.Parse("aaaa0001-0000-0000-0000-00000000000a");

    private static readonly Guid DemoPayerPartyId    = Guid.Parse("bfe9921e-2f3e-4c56-b8d1-4f5b2a7c3d44");
    private static readonly Guid DemoReceiverPartyId = Guid.Parse("2a3e1f59-44f7-4df4-a8f1-936f9d9d13cd");
    private static readonly Guid TundePartyIdRef     = Guid.Parse("5ef5e008-8d3d-485f-8718-67ab4d4da2cf");
    private static readonly Guid AdwoaPartyIdRef     = Guid.Parse("5c882622-4958-4e0e-8cad-cb20f6e720ca");
    private static readonly Guid PeterPartyIdRef     = Guid.Parse("cb94f5cd-ed2d-4e95-99be-6d8bb6acdbbe");
    private static readonly Guid NalediPartyIdRef    = Guid.Parse("40ee8396-c640-4d0a-a262-2d32743cb95a");
    private static readonly Guid KofiPartyIdRef      = Guid.Parse("563b6348-c34f-423f-8b22-c92ca6f9f195");
    private static readonly Guid AcmeImportsPartyIdRef = Guid.Parse("f0f72256-f43b-455a-af08-8fab70115794");
    private static readonly Guid OliviaPartyIdRef    = Guid.Parse("fb229001-e24c-4fd3-a87d-e0458a2cf8cb");
    private static readonly Guid LiamPartyIdRef      = Guid.Parse("3f48a4fc-c7ce-4f78-af09-a2796e735f85");

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

            // Replace items + party roles each run — cheap for demo volumes
            // and avoids tracking conflicts on rerun.
            var existingItems = await _financeDbContext.OrderItems
                .Where(i => i.OrderId == seed.OrderId)
                .ToListAsync(cancellationToken);
            if (existingItems.Count > 0) _financeDbContext.OrderItems.RemoveRange(existingItems);

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

            var existingRoles = await _financeDbContext.OrderPartyRoles
                .Where(r => r.OrderId == seed.OrderId)
                .ToListAsync(cancellationToken);
            if (existingRoles.Count > 0) _financeDbContext.OrderPartyRoles.RemoveRange(existingRoles);

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
