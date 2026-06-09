using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Catalog;
using Aonik.Finance.Services.Partners.Connectors;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Primitives;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Catalog;

public class BillerImportServiceTests
{
    // ── Test doubles ──────────────────────────────────────────────────────────
    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(new List<string>());
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? ResolutionSource { get; set; } = "Test";
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid id) { id = userId; return true; }
    }

    /// <summary>Returns canned catalogue entries; honours the BillerCodes selection filter like the real connector.</summary>
    private sealed class FakeBillConnector : IPartnerBillPaymentConnector
    {
        public List<BillerCatalogEntry> Entries { get; set; } = new();

        public string ProviderCode => "Flutterwave";
        public IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; } = new[]
        {
            new PartnerConnectorCapability(PartnerServiceCategory.BillPayment, new[] { "NG" }, new[] { "NGN" }, new[] { "Bill" }),
        };

        public Task<IReadOnlyList<BillerCatalogEntry>> GetBillerCatalogAsync(
            BillerCatalogQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<BillerCatalogEntry> result = Entries;
            if (query.BillerCodes is { Count: > 0 })
            {
                var selected = new HashSet<string>(query.BillerCodes, StringComparer.OrdinalIgnoreCase);
                result = result.Where(e => selected.Contains(e.BillerCode));
            }

            return Task.FromResult<IReadOnlyList<BillerCatalogEntry>>(result.ToList());
        }

        public Task<BillCustomerValidationResult> ValidateCustomerAsync(BillCustomerValidationRequest r, CancellationToken c = default)
            => throw new NotSupportedException();
        public Task<BillPaymentResult> PayBillAsync(BillPaymentInstruction i, CancellationToken c = default)
            => throw new NotSupportedException();
        public Task<BillPaymentStatusResult> GetBillPaymentStatusAsync(PartnerReference r, CancellationToken c = default)
            => throw new NotSupportedException();
    }

    // ── Fixture ───────────────────────────────────────────────────────────────
    private sealed record Fixture(
        FinanceDbContext Context, BillerImportService Service, FakeBillConnector Connector, Guid ConnectorId);

    private static Fixture CreateFixture(Guid tenantId, FakeBillConnector connector)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        var context = new FinanceDbContext(options, new TestTenantProvider(tenantId));

        var connectorId = Guid.NewGuid();
        context.Connectors.Add(new Connector
        {
            Id = connectorId,
            TenantId = tenantId,
            PartnerId = Guid.NewGuid(),
            ConnectorType = "Flutterwave",
            ConfigJson = "{}",
            Status = "Active"
        });
        context.SaveChanges();

        var resolver = new Mock<IPartnerConnectorResolver>();
        resolver.Setup(r => r.ResolveBillPaymentConnector(It.IsAny<string>())).Returns(connector);

        // Spec 042: BillerImportService now binds the bill connector to the operator-selected row via the
        // factory; the stub returns the test connector for any row (IPartnerConnectorFactory is internal,
        // so it cannot be Moq-proxied).
        var factory = new Spec042StubConnectorFactory(billConnector: connector);

        var service = new BillerImportService(
            context,
            resolver.Object,
            factory,
            new IPartnerBillPaymentConnector[] { connector },
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestTenantContext(tenantId),
            new TestClock());

        return new Fixture(context, service, connector, connectorId);
    }

    private static BillerCatalogEntry Entry(
        string billerCode, string billerName, string category, PartnerServiceCategory serviceCategory, params BillItem[] items)
        => new(billerCode, billerName, category.ToUpperInvariant(), category, serviceCategory,
            new[] { new BillCustomerField("customer", "Customer ID", true) }, items);

    private static BillItem FixedItem(string code, string name, decimal amount)
        => new(code, name, BillAmountType.Fixed, new Money(amount, "NGN"), null, null);

    private static BillItem VariableItem(string code, string name)
        => new(code, name, BillAmountType.Variable, null, null, null);

    private static BillerImportRequest Select(Guid connectorId, params string[] billerCodes)
        => new(connectorId, billerCodes.Select(c => new BillerImportSelector(c)).ToList());

    // ── Tests ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ImportAsync_Should_CreateBillersServicesAndMappings_OnFirstImport()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL099", "MTN Nigeria", "Airtime", PartnerServiceCategory.AirtimeTopup,
                    VariableItem("AT099", "Airtime"), FixedItem("DATA1", "1GB Data", 500m))
            }
        };
        var fx = CreateFixture(tenantId, connector);

        var summary = await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL099"));

        summary.BillersCreated.Should().Be(1);
        summary.ServicesCreated.Should().Be(2);
        summary.BillersUpdated.Should().Be(0);
        summary.Deactivated.Should().Be(0);

        (await fx.Context.CatalogBillers.CountAsync()).Should().Be(1);
        (await fx.Context.CatalogBillerServices.CountAsync()).Should().Be(2);
        (await fx.Context.ConnectorBillerMappings.CountAsync()).Should().Be(3); // 1 biller-level + 2 service-level
        (await fx.Context.CatalogBillerCategories.CountAsync()).Should().Be(1);

        // ServiceCode is an AONIK logical slug, never the provider item code (Spec 040 §7).
        var serviceCodes = await fx.Context.CatalogBillerServices.Select(s => s.ServiceCode).ToListAsync();
        serviceCodes.Should().NotContain(new[] { "AT099", "DATA1" });

        var billerMapping = await fx.Context.ConnectorBillerMappings
            .SingleAsync(m => m.CatalogBillerServiceId == null);
        billerMapping.LastSyncedAt.Should().NotBeNull();
        billerMapping.ProviderBillerCode.Should().Be("BIL099");
    }

    [Fact]
    public async Task ImportAsync_Should_BeIdempotent_OnReimport()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL099", "MTN Nigeria", "Airtime", PartnerServiceCategory.AirtimeTopup,
                    VariableItem("AT099", "Airtime"), FixedItem("DATA1", "1GB Data", 500m))
            }
        };
        var fx = CreateFixture(tenantId, connector);

        await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL099"));
        var second = await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL099"));

        second.BillersCreated.Should().Be(0);
        second.ServicesCreated.Should().Be(0);
        second.BillersUpdated.Should().Be(1);
        second.ServicesUpdated.Should().Be(2);
        second.Deactivated.Should().Be(0);

        (await fx.Context.CatalogBillers.CountAsync()).Should().Be(1);
        (await fx.Context.CatalogBillerServices.CountAsync()).Should().Be(2);
        (await fx.Context.ConnectorBillerMappings.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task ImportAsync_Should_UpsertChangedNameAndAmount_InPlace()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL112", "Ikeja Electric", "Utility", PartnerServiceCategory.BillPayment,
                    FixedItem("BUNDLE", "Power Bundle", 5000m))
            }
        };
        var fx = CreateFixture(tenantId, connector);
        await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL112"));

        // Partner changes the biller name and the bundle price.
        connector.Entries =
        [
            Entry("BIL112", "Ikeja Electric PLC", "Utility", PartnerServiceCategory.BillPayment,
                FixedItem("BUNDLE", "Power Bundle", 7500m))
        ];

        var summary = await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL112"));

        summary.BillersUpdated.Should().Be(1);
        summary.ServicesUpdated.Should().Be(1);
        summary.BillersCreated.Should().Be(0);

        var biller = await fx.Context.CatalogBillers.SingleAsync();
        biller.Name.Should().Be("Ikeja Electric PLC");

        var service = await fx.Context.CatalogBillerServices.SingleAsync();
        service.FixedAmount.Should().Be(7500m);

        (await fx.Context.CatalogBillers.CountAsync()).Should().Be(1);
        (await fx.Context.CatalogBillerServices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_Should_SoftDeactivateDroppedService_NotDelete()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL099", "MTN Nigeria", "Airtime", PartnerServiceCategory.AirtimeTopup,
                    VariableItem("AT099", "Airtime"), FixedItem("DATA1", "1GB Data", 500m))
            }
        };
        var fx = CreateFixture(tenantId, connector);
        await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL099"));

        // Partner drops the data bundle.
        connector.Entries =
        [
            Entry("BIL099", "MTN Nigeria", "Airtime", PartnerServiceCategory.AirtimeTopup,
                VariableItem("AT099", "Airtime"))
        ];

        var summary = await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL099"));

        summary.Deactivated.Should().Be(1);

        // Nothing hard-deleted — the dropped service survives as inactive.
        (await fx.Context.CatalogBillerServices.CountAsync()).Should().Be(2);
        (await fx.Context.ConnectorBillerMappings.CountAsync()).Should().Be(3);

        var droppedMapping = await fx.Context.ConnectorBillerMappings
            .SingleAsync(m => m.ProviderItemCode == "DATA1");
        droppedMapping.IsActive.Should().BeFalse();

        var droppedService = await fx.Context.CatalogBillerServices
            .SingleAsync(s => s.Id == droppedMapping.CatalogBillerServiceId);
        droppedService.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task PreviewAsync_Should_TagEntriesAndPersistNothing()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL099", "MTN Nigeria", "Airtime", PartnerServiceCategory.AirtimeTopup,
                    VariableItem("AT099", "Airtime"))
            }
        };
        var fx = CreateFixture(tenantId, connector);

        // Before import: New, and nothing written.
        var first = await fx.Service.PreviewAsync(new BillerImportPreviewRequest(fx.ConnectorId));
        first.Entries.Should().ContainSingle().Which.ImportStatus.Should().Be("New");
        (await fx.Context.CatalogBillers.CountAsync()).Should().Be(0);

        await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL099"));

        // After import: Mapped.
        var mapped = await fx.Service.PreviewAsync(new BillerImportPreviewRequest(fx.ConnectorId));
        mapped.Entries.Should().ContainSingle().Which.ImportStatus.Should().Be("Mapped");

        // Partner renames the biller → Changed.
        connector.Entries =
        [
            Entry("BIL099", "MTN Nigeria Communications", "Airtime", PartnerServiceCategory.AirtimeTopup,
                VariableItem("AT099", "Airtime"))
        ];
        var changed = await fx.Service.PreviewAsync(new BillerImportPreviewRequest(fx.ConnectorId));
        changed.Entries.Should().ContainSingle().Which.ImportStatus.Should().Be("Changed");
    }

    [Fact]
    public async Task ImportAsync_Should_RespectSelectedItemCodes_AndNotImportUnselectedItems()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL112", "Ikeja Electric", "Utility", PartnerServiceCategory.BillPayment,
                    VariableItem("PREPAID", "Prepaid"), FixedItem("BUNDLE", "Power Bundle", 5000m))
            }
        };
        var fx = CreateFixture(tenantId, connector);

        // Operator selects only the PREPAID item under the biller.
        var summary = await fx.Service.ImportAsync(new BillerImportRequest(
            fx.ConnectorId,
            new List<BillerImportSelector> { new("BIL112", new List<string> { "PREPAID" }) }));

        summary.ServicesCreated.Should().Be(1);
        (await fx.Context.CatalogBillerServices.CountAsync()).Should().Be(1);

        var serviceMappings = await fx.Context.ConnectorBillerMappings
            .Where(m => m.CatalogBillerServiceId != null).ToListAsync();
        serviceMappings.Should().ContainSingle().Which.ProviderItemCode.Should().Be("PREPAID");

        // 1 biller-level mapping + 1 service-level mapping; the unselected BUNDLE is not exposed.
        (await fx.Context.ConnectorBillerMappings.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ImportAsync_Should_NotDeactivateOfferedItemsLeftOutOfSelection()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector
        {
            Entries =
            {
                Entry("BIL112", "Ikeja Electric", "Utility", PartnerServiceCategory.BillPayment,
                    VariableItem("PREPAID", "Prepaid"), FixedItem("BUNDLE", "Power Bundle", 5000m))
            }
        };
        var fx = CreateFixture(tenantId, connector);

        // First import the whole biller (no item codes ⇒ all items).
        await fx.Service.ImportAsync(Select(fx.ConnectorId, "BIL112"));

        // Re-import selecting only PREPAID. BUNDLE is still offered by the partner — just not selected,
        // so it must stay active (not soft-deactivated).
        var summary = await fx.Service.ImportAsync(new BillerImportRequest(
            fx.ConnectorId,
            new List<BillerImportSelector> { new("BIL112", new List<string> { "PREPAID" }) }));

        summary.Deactivated.Should().Be(0);
        summary.ServicesUpdated.Should().Be(1); // only PREPAID was touched
        (await fx.Context.CatalogBillerServices.CountAsync()).Should().Be(2);

        var bundleMapping = await fx.Context.ConnectorBillerMappings.SingleAsync(m => m.ProviderItemCode == "BUNDLE");
        bundleMapping.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetSourcesAsync_Should_ReturnConnectorsWithBillPaymentSupport()
    {
        var tenantId = Guid.NewGuid();
        var connector = new FakeBillConnector(); // ProviderCode "Flutterwave"
        var fx = CreateFixture(tenantId, connector);

        var sources = await fx.Service.GetSourcesAsync();

        var source = sources.Sources.Should().ContainSingle().Subject;
        source.ConnectorId.Should().Be(fx.ConnectorId);
        source.ConnectorType.Should().Be("Flutterwave");
        source.IsSandbox.Should().BeFalse();
    }
}
