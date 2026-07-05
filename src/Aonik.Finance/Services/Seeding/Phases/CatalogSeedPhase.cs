using System.Text.Json;

using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

using Aonik.SharedKernel.Seeding;

namespace Aonik.Finance.Services.Seeding.Phases;

/// <summary>
/// Seeds the domestic Ghana biller catalog (ECG Power, Ghana Water) plus
/// a tenant-scoped Utilities category. Reads the bill-collection partner ID
/// from <paramref name="results"/> set by <see cref="BillCollectionPartnerSeedPhase"/>.
/// </summary>
internal sealed class CatalogSeedPhase
{
    private static readonly FinanceDemoSeedIds SeedIds = FinanceDemoSeedIds.Instance;

    private readonly FinanceDbContext _db;
    private readonly CatalogUpsertHelper _catalog;

    public CatalogSeedPhase(
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

        var billCollectionPartnerId = (Guid)results[DemoSeedResultKeys.BillCollectionPartnerId];

        var categoryId = await _catalog.UpsertCategoryAsync(
            tenantId, SeedIds.Catalog.UtilitiesCategoryId, "GH", "Utilities",
            "Electricity and water billers", 1, now, userId, operations, cancellationToken);

        var ecgBillerId = await _catalog.UpsertBillerAsync(
            tenantId, categoryId, SeedIds.Catalog.EcgBillerId,
            "ECG Power", "Ghana's electricity provider.",
            now, userId, operations, cancellationToken, billCollectionPartnerId);

        var waterBillerId = await _catalog.UpsertBillerAsync(
            tenantId, categoryId, SeedIds.Catalog.GhanaWaterBillerId,
            "Ghana Water", "National water utility.",
            now, userId, operations, cancellationToken, billCollectionPartnerId);

        var ecgServiceId = await _catalog.UpsertServiceAsync(
            tenantId, ecgBillerId, SeedIds.Catalog.EcgPrepaidServiceId,
            "BILLPAY.ELECTRICITY.PREPAID", "ECG Prepaid Electricity", "Prepaid", "GHS",
            5, 500, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{SeedIds.Catalog.EcgBillerId}/services/{SeedIds.Catalog.EcgPrepaidServiceId}/validate",
                "precheck")),
            operations, cancellationToken);

        var waterServiceId = await _catalog.UpsertServiceAsync(
            tenantId, waterBillerId, SeedIds.Catalog.GhanaWaterServiceId,
            "BILLPAY.WATER.POSTPAID", "Ghana Water Postpaid", "Postpaid", "GHS",
            10, 1000, false, false,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 6, 20, null, "Enter account number", null)
            }),
            null, operations, cancellationToken);

        await _catalog.UpsertServiceAsync(
            tenantId, ecgBillerId, SeedIds.Catalog.EcgPostpaidServiceId,
            "BILLPAY.ELECTRICITY.POSTPAID.GH.ECG", "ECG Postpaid Electricity", "Postpaid", "GHS",
            20, 2000, false, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("accountNumber", "Account number", "text", true, 8, 20, null, "Enter account number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{SeedIds.Catalog.EcgBillerId}/services/{SeedIds.Catalog.EcgPostpaidServiceId}/validate",
                "precheck")),
            operations, cancellationToken);

        await _catalog.UpsertServiceAsync(
            tenantId, waterBillerId, SeedIds.Catalog.GhanaWaterPrepaidServiceId,
            "BILLPAY.WATER.PREPAID.GH.GWL", "Ghana Water Prepaid", "Prepaid", "GHS",
            5, 1000, true, true,
            CatalogUpsertHelper.BuildServiceFieldsJson(new[]
            {
                new CatalogServiceField("meterNumber", "Meter number", "text", true, 6, 16, null, "Enter meter number", null),
                new CatalogServiceField("customerName", "Customer name", "text", true, 2, 80, null, "Enter customer name", null)
            }),
            JsonSerializer.Serialize(new CatalogServiceValidation(
                $"/catalog/billers/{SeedIds.Catalog.GhanaWaterBillerId}/services/{SeedIds.Catalog.GhanaWaterPrepaidServiceId}/validate",
                "precheck")),
            operations, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        results[DemoSeedResultKeys.UtilitiesCategoryId] = categoryId;
        results[DemoSeedResultKeys.EcgBillerId] = ecgBillerId;
        results[DemoSeedResultKeys.WaterBillerId] = waterBillerId;
        results[DemoSeedResultKeys.EcgServiceId] = ecgServiceId;
        results[DemoSeedResultKeys.WaterServiceId] = waterServiceId;

        return operations;
    }
}
