using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Finance.Services.Integration;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance;

/// <summary>
/// Spec 088 P2/P3 acceptance: an order-backed invoice persists its link, a standalone invoice
/// still persists null, and a repeated idempotency key returns the original invoice rather than
/// billing the customer twice.
///
/// The link was silently dropped before this. <c>InvoiceWriter</c> used <c>command.OrderId</c>
/// only to derive an invoice number, and <c>CreateInvoiceRequest</c> had no field to carry it, so
/// <c>Invoice.OrderId</c> stayed null for every invoice ever raised. Settlement routing (§9) reads
/// the funding order's type through this link and invoice idempotency (§8) keys on it, so both
/// were unimplementable until now.
/// </summary>
public class InvoiceOrderLinkTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId = Guid.NewGuid();
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = _userId; return true; }
    }

    private static (BillingService Service, FinanceDbContext Db) CreateService()
    {
        var tenantId = Guid.NewGuid();
        var context = new FinanceDbContext(
            new DbContextOptionsBuilder<FinanceDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(tenantId));

        var service = new BillingService(
            context,
            new TestTenantProvider(tenantId),
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(),
            new FinanceMetrics(),
            new LedgerPostingService(context));

        return (service, context);
    }

    private static CreateInvoiceRequest Request(Guid? orderId, string? idempotencyKey = null) =>
        new(
            CustomerId: Guid.NewGuid(),
            InvoiceNumber: "INV-TEST",
            Currency: "GBP",
            DueUtc: new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            LineItems: [new CreateInvoiceLineItemRequest("Family plan, August", 1, 19.99m)],
            OrderId: orderId,
            IdempotencyKey: idempotencyKey);

    [Fact]
    public async Task CreateInvoiceAsync_Should_PersistTheOrderLink_When_OneIsSupplied()
    {
        var (service, db) = CreateService();
        var orderId = Guid.NewGuid();

        var response = await service.CreateInvoiceAsync(Request(orderId));

        response.OrderId.Should().Be(orderId);

        var persisted = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == response.Id);
        persisted.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task CreateInvoiceAsync_Should_PersistNull_When_TheInvoiceIsStandalone()
    {
        var (service, db) = CreateService();

        var response = await service.CreateInvoiceAsync(Request(orderId: null));

        response.OrderId.Should().BeNull();

        var persisted = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == response.Id);
        persisted.OrderId.Should().BeNull();
    }

    [Fact]
    public async Task CreateInvoiceAsync_Should_AllowTwoStandaloneInvoices_ForOneTenant()
    {
        var (service, db) = CreateService();

        var first = await service.CreateInvoiceAsync(Request(orderId: null));
        var second = await service.CreateInvoiceAsync(Request(orderId: null));

        // Standalone invoices are legitimate, which is exactly why the unique index this link
        // enables must be FILTERED (§8): an unfiltered unique index on (TenantId, OrderId)
        // permits one NULL per tenant and would reject the second of these.
        first.Id.Should().NotBe(second.Id);
        (await db.Invoices.CountAsync(i => i.OrderId == null)).Should().Be(2);
    }

    [Fact]
    public async Task InvoiceWriter_Should_PersistTheOrderLink_ForCrossModuleCallers()
    {
        var (service, db) = CreateService();
        var writer = new InvoiceWriter(service);
        var orderId = Guid.NewGuid();

        // The path a module outside Finance actually uses — and the one that dropped the link.
        var reference = await writer.CreateForOrderAsync(new CreateInvoiceForOrderCommand(
            OrderId: orderId,
            CustomerId: Guid.NewGuid(),
            Currency: "GBP",
            Lines: [new InvoiceLineSpec("Family plan, August", 1, 19.99m)]));

        var persisted = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == reference.InvoiceId);
        persisted.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task CreateInvoiceAsync_Should_ReturnTheOriginal_When_TheIdempotencyKeyRepeats()
    {
        var (service, db) = CreateService();
        var orderId = Guid.NewGuid();

        var first = await service.CreateInvoiceAsync(Request(orderId, "sub:1:period:7"));
        var second = await service.CreateInvoiceAsync(Request(orderId, "sub:1:period:7"));

        // A renewal job that dies after raising the invoice but before recording its id retries.
        // Returning the original is what stops it billing the customer a second time; the filtered
        // unique index is the backstop for the concurrent case (covered on LocalDB).
        second.Id.Should().Be(first.Id);
        (await db.Invoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateInvoiceAsync_Should_TreatKeylessRequests_AsDistinct()
    {
        var (service, db) = CreateService();

        await service.CreateInvoiceAsync(Request(Guid.NewGuid()));
        await service.CreateInvoiceAsync(Request(Guid.NewGuid()));

        // Idempotency is opt-in: a caller that supplies no key gets no deduplication, which is how
        // every existing call site continues to behave.
        (await db.Invoices.CountAsync()).Should().Be(2);
    }
}
