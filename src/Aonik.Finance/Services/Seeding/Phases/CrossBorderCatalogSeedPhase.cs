using System.Text.Json;

using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

using Aonik.SharedKernel.Seeding;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds the cross-border biller catalog for NG, GH, KE, and ZA corridors.
/// Reads partner IDs from <paramref name="results"/> set by
/// <see cref="CrossBorderPartnerNetworkSeedPhase"/>.
/// </summary>
internal sealed class CrossBorderCatalogSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly CatalogUpsertHelper _catalog;

    public CrossBorderCatalogSeedPhase(
        FinanceDbContext db,
        CatalogUpsertHelper catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<string>> SeedAsync(
        DemoSeedContext context,
        Dictionary<string, object> results,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var now = context.Now;
        var userId = context.UserId;
        var operations = new List<string>();

        var partnerIdsByCountry = (Dictionary<string, Guid>)results[DemoSeedResultKeys.PartnerIdsByCountry];

        if (!partnerIdsByCountry.TryGetValue("GH", out var ghPartnerId))
            throw new InvalidOperationException("Cross-border partner for GH is required.");
        if (!partnerIdsByCountry.TryGetValue("NG", out var ngPartnerId))
            throw new InvalidOperationException("Cross-border partner for NG is required.");
        if (!partnerIdsByCountry.TryGetValue("KE", out var kePartnerId))
            throw new InvalidOperationException("Cross-border partner for KE is required.");
        if (!partnerIdsByCountry.TryGetValue("ZA", out var zaPartnerId))
            throw new InvalidOperationException("Cross-border partner for ZA is required.");

        var ghCategoryId = await _catalog.UpsertCategoryAsync(tenantId, SeedIds.Catalog.UtilitiesCategoryId, "GH", "Utilities", "Electricity and water billers", 1, now, userId, operations, cancellationToken);
        var ngCategoryId = await _catalog.UpsertCategoryAsync(tenantId, SeedIds.CrossBorderCategories.NigeriaUtilitiesCategoryId, "NG", "Utilities", "Electricity and water billers in Nigeria", 1, now, userId, operations, cancellationToken);
        var keCategoryId = await _catalog.UpsertCategoryAsync(tenantId, SeedIds.CrossBorderCategories.KenyaUtilitiesCategoryId, "KE", "Utilities", "Electricity billers in Kenya", 1, now, userId, operations, cancellationToken);
        var zaCategoryId = await _catalog.UpsertCategoryAsync(tenantId, SeedIds.CrossBorderCategories.SouthAfricaUtilitiesCategoryId, "ZA", "Utilities", "Electricity billers in South Africa", 1, now, userId, operations, cancellationToken);

        var ecgBillerId = await _catalog.UpsertBillerAsync(tenantId, ghCategoryId, SeedIds.Catalog.EcgBillerId, "ECG Power", "Ghana's electricity provider.", now, userId, operations, cancellationToken, ghPartnerId, "GH");
        var ghWaterBillerId = await _catalog.UpsertBillerAsync(tenantId, ghCategoryId, SeedIds.Catalog.GhanaWaterBillerId, "Ghana Water", "National water utility.", now, userId, operations, cancellationToken, ghPartnerId, "GH");
        var ikejaBillerId = await _catalog.UpsertBillerAsync(tenantId, ngCategoryId, SeedIds.CrossBorderBillers.IkejaElectricBillerId, "Ikeja Electric", "Prepaid electricity in Lagos.", now, userId, operations, cancellationToken, ngPartnerId, "NG");
        var lagosWaterBillerId = await _catalog.UpsertBillerAsync(tenantId, ngCategoryId, SeedIds.CrossBorderBillers.LagosWaterBillerId, "Lagos Water Board", "Postpaid water services for Lagos residents.", now, userId, operations, cancellationToken, ngPartnerId, "NG");
        var kenyaPowerBillerId = await _catalog.UpsertBillerAsync(tenantId, keCategoryId, SeedIds.CrossBorderBillers.KenyaPowerBillerId, "Kenya Power", "National electricity distribution utility.", now, userId, operations, cancellationToken, kePartnerId, "KE");
        var cityPowerBillerId = await _catalog.UpsertBillerAsync(tenantId, zaCategoryId, SeedIds.CrossBorderBillers.CityPowerBillerId, "City Power Johannesburg", "Municipal electricity provider.", now, userId, operations, cancellationToken, zaPartnerId, "ZA");

        var ecgServiceId = await _catalog.UpsertServiceAsync(tenantId, ecgBillerId, SeedIds.Catalog.EcgPrepaidServiceId, "BILLPAY.ELECTRICITY.PREPAID", "ECG Prepaid Electricity", "Prepaid", "GHS", 5, 500, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.Catalog.EcgBillerId}/services/{SeedIds.Catalog.EcgPrepaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var ghWaterServiceId = await _catalog.UpsertServiceAsync(tenantId, ghWaterBillerId, SeedIds.Catalog.GhanaWaterServiceId, "BILLPAY.WATER.POSTPAID", "Ghana Water Postpaid", "Postpaid", "GHS", 10, 1000, false, false,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 6, 20, null, "Enter account number", null) }),
            null, operations, cancellationToken);

        var ikejaServiceId = await _catalog.UpsertServiceAsync(tenantId, ikejaBillerId, SeedIds.CrossBorderServices.IkejaPrepaidServiceId, "BILLPAY.ELECTRICITY.PREPAID.NG.IKEJA", "Ikeja Prepaid Electricity", "Prepaid", "NGN", 500, 250000, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.IkejaElectricBillerId}/services/{SeedIds.CrossBorderServices.IkejaPrepaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var ikejaPostpaidServiceId = await _catalog.UpsertServiceAsync(tenantId, ikejaBillerId, SeedIds.CrossBorderServices.IkejaPostpaidServiceId, "BILLPAY.ELECTRICITY.POSTPAID.NG.IKEJA", "Ikeja Postpaid Electricity", "Postpaid", "NGN", 1000, 400000, false, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.IkejaElectricBillerId}/services/{SeedIds.CrossBorderServices.IkejaPostpaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var lagosWaterServiceId = await _catalog.UpsertServiceAsync(tenantId, lagosWaterBillerId, SeedIds.CrossBorderServices.LagosWaterServiceId, "BILLPAY.WATER.POSTPAID.NG.LAGOS", "Lagos Water Postpaid", "Postpaid", "NGN", 1000, 150000, false, false,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null) }),
            null, operations, cancellationToken);

        var lagosWaterPrepaidServiceId = await _catalog.UpsertServiceAsync(tenantId, lagosWaterBillerId, SeedIds.CrossBorderServices.LagosWaterPrepaidServiceId, "BILLPAY.WATER.PREPAID.NG.LAGOS", "Lagos Water Prepaid", "Prepaid", "NGN", 500, 150000, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.LagosWaterBillerId}/services/{SeedIds.CrossBorderServices.LagosWaterPrepaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var kenyaPowerServiceId = await _catalog.UpsertServiceAsync(tenantId, kenyaPowerBillerId, SeedIds.CrossBorderServices.KenyaPowerServiceId, "BILLPAY.ELECTRICITY.PREPAID.KE.KPLC", "Kenya Power Prepaid", "Prepaid", "KES", 100, 150000, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("nationalId", "National ID", "text", true, 6, 12, null, "Enter national ID", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.KenyaPowerBillerId}/services/{SeedIds.CrossBorderServices.KenyaPowerServiceId}/validate", "precheck")), operations, cancellationToken);

        var kenyaPowerPostpaidServiceId = await _catalog.UpsertServiceAsync(tenantId, kenyaPowerBillerId, SeedIds.CrossBorderServices.KenyaPowerPostpaidServiceId, "BILLPAY.ELECTRICITY.POSTPAID.KE.KPLC", "Kenya Power Postpaid", "Postpaid", "KES", 250, 200000, false, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null), new CatalogServiceField("nationalId", "National ID", "text", true, 6, 12, null, "Enter national ID", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.KenyaPowerBillerId}/services/{SeedIds.CrossBorderServices.KenyaPowerPostpaidServiceId}/validate", "precheck")), operations, cancellationToken);

        var cityPowerServiceId = await _catalog.UpsertServiceAsync(tenantId, cityPowerBillerId, SeedIds.CrossBorderServices.CityPowerServiceId, "BILLPAY.ELECTRICITY.PREPAID.ZA.CPJ", "City Power Prepaid", "Prepaid", "ZAR", 10, 25000, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null), new CatalogServiceField("surname", "Surname", "text", true, 2, 80, null, "Enter surname", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.CityPowerBillerId}/services/{SeedIds.CrossBorderServices.CityPowerServiceId}/validate", "precheck")), operations, cancellationToken);

        var cityPowerPostpaidServiceId = await _catalog.UpsertServiceAsync(tenantId, cityPowerBillerId, SeedIds.CrossBorderServices.CityPowerPostpaidServiceId, "BILLPAY.ELECTRICITY.POSTPAID.ZA.CPJ", "City Power Postpaid", "Postpaid", "ZAR", 50, 50000, false, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[] { new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null), new CatalogServiceField("surname", "Surname", "text", true, 2, 80, null, "Enter surname", null) }),
            JsonSerializer.Serialize(new CatalogServiceValidation($"/catalog/billers/{SeedIds.CrossBorderBillers.CityPowerBillerId}/services/{SeedIds.CrossBorderServices.CityPowerPostpaidServiceId}/validate", "precheck")), operations, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        operations.Add("Extended catalog for NG, GH, KE, and ZA bill collection corridors");

        results[DemoSeedResultKeys.CrossBorderCategoryIds] = new[] { ghCategoryId, ngCategoryId, keCategoryId, zaCategoryId };
        results[DemoSeedResultKeys.CrossBorderBillerIds] = new[] { ecgBillerId, ghWaterBillerId, ikejaBillerId, lagosWaterBillerId, kenyaPowerBillerId, cityPowerBillerId };
        results[DemoSeedResultKeys.CrossBorderServiceIds] = new[]
        {
            ecgServiceId, ghWaterServiceId, ikejaServiceId, ikejaPostpaidServiceId,
            lagosWaterServiceId, lagosWaterPrepaidServiceId, kenyaPowerServiceId,
            kenyaPowerPostpaidServiceId, cityPowerServiceId, cityPowerPostpaidServiceId
        };

        return operations;
    }
}
