using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

/// <summary>
/// #221 (M7-style defense-in-depth): <see cref="PublicPaymentService"/> is the
/// guest/checkout surface (<c>AllowAnonymous</c> endpoints) — the same class of
/// sensitive by-id order/intent lookup #120 hardened for the authenticated
/// <c>PaymentService</c>. Mirrors <c>PaymentServiceTests</c>'s discriminating-test
/// technique: seed under tenant A (the DbContext's own query filter is bound to A),
/// then read back through a service resolving a different ambient tenant B. The
/// global filter alone would allow the row through; the explicit predicate must
/// still exclude it.
/// </summary>
public class PublicPaymentServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class FakeGateway : IPaymentProviderGateway
    {
        public string ProviderCode { get; init; } = "Stripe";

        public Task<PaymentProviderIntentResult> CreateIntentAsync(
            PaymentProviderIntentRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentProviderIntentResult(
                ProviderCode, $"pi_{request.OrderId:N}", "RequiresAction", "secret_123", "https://checkout.example.com/pi_123"));

        public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(
            PaymentProviderSetupIntentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static PublicPaymentService CreateService(FinanceDbContext context, Guid tenantId) =>
        new(context, new TestTenantProvider(tenantId), [new FakeGateway()]);

    private static async Task<Order> SeedOrderAsync(
        FinanceDbContext context,
        Guid tenantId,
        string orderType = "BillPayment",
        string status = "Draft",
        decimal amountIn = 100m,
        Guid? payerPartyId = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = orderType,
            PayerPartyId = payerPartyId,
            AmountIn = amountIn,
            CurrencyIn = "USD",
            Status = status,
            FeesJson = "[]",
            ProvenanceJson = "{}"
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    // ── CreateGuestPaymentIntentAsync ──────────────────────────────────────

    [Fact]
    public async Task CreateGuestPaymentIntentAsync_Should_CreateIntent_ForValidBillPaymentOrder()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var order = await SeedOrderAsync(context, tenantId);
        var service = CreateService(context, tenantId);

        var result = await service.CreateGuestPaymentIntentAsync(
            new CreateGuestPaymentIntentRequest(order.Id, "Stripe", "Card", null, null));

        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);

        var saved = await context.PaymentIntents.FirstAsync(p => p.Id == result.PaymentIntentId);
        saved.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreateGuestPaymentIntentAsync_Should_Throw_When_OrderNotFound()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var act = async () => await service.CreateGuestPaymentIntentAsync(
            new CreateGuestPaymentIntentRequest(Guid.NewGuid(), "Stripe", "Card", null, null));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateGuestPaymentIntentAsync_Should_NotReturn_OtherTenantsOrder_EvenWhenQueryFilterWouldAllowIt()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var context = CreateDbContext(tenantA); // global filter bound to tenant A

        var order = await SeedOrderAsync(context, tenantA);

        // Same context (filter still A), but the service now acts as tenant B.
        var act = async () => await CreateService(context, tenantB).CreateGuestPaymentIntentAsync(
            new CreateGuestPaymentIntentRequest(order.Id, "Stripe", "Card", null, null));

        await act.Should().ThrowAsync<NotFoundException>(
            "the explicit TenantId predicate must exclude another tenant's order even when the global query filter would allow it");
    }

    // ── CreateCommerceGuestPaymentIntentAsync ──────────────────────────────

    [Fact]
    public async Task CreateCommerceGuestPaymentIntentAsync_Should_CreateIntent_ForValidProductPurchaseOrder()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var order = await SeedOrderAsync(context, tenantId, orderType: "ProductPurchase");
        var service = CreateService(context, tenantId);

        var result = await service.CreateCommerceGuestPaymentIntentAsync(
            new CreateCommerceGuestPaymentIntentRequest(order.Id, 42.50m, "USD", "Stripe", "Card", null, null));

        result.Should().NotBeNull();
        result.Amount.Should().Be(42.50m);

        var saved = await context.PaymentIntents.FirstAsync(p => p.Id == result.PaymentIntentId);
        saved.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreateCommerceGuestPaymentIntentAsync_Should_NotReturn_OtherTenantsOrder_EvenWhenQueryFilterWouldAllowIt()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var context = CreateDbContext(tenantA);

        var order = await SeedOrderAsync(context, tenantA, orderType: "ProductPurchase");

        var act = async () => await CreateService(context, tenantB).CreateCommerceGuestPaymentIntentAsync(
            new CreateCommerceGuestPaymentIntentRequest(order.Id, 42.50m, "USD", "Stripe", "Card", null, null));

        await act.Should().ThrowAsync<NotFoundException>(
            "the explicit TenantId predicate must exclude another tenant's order even when the global query filter would allow it");
    }

    // ── GetGuestPaymentIntentStatusAsync ────────────────────────────────────

    [Fact]
    public async Task GetGuestPaymentIntentStatusAsync_Should_ReturnStatus_WhenExists()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var order = await SeedOrderAsync(context, tenantId);
        var service = CreateService(context, tenantId);
        var created = await service.CreateGuestPaymentIntentAsync(
            new CreateGuestPaymentIntentRequest(order.Id, "Stripe", "Card", null, null));

        var result = await service.GetGuestPaymentIntentStatusAsync(
            new GetGuestPaymentIntentStatusRequest(order.Id, created.PaymentIntentId, null));

        result.Should().NotBeNull();
        result!.PaymentIntentId.Should().Be(created.PaymentIntentId);
    }

    [Fact]
    public async Task GetGuestPaymentIntentStatusAsync_Should_ReturnNull_When_OrderNotFound()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var result = await service.GetGuestPaymentIntentStatusAsync(
            new GetGuestPaymentIntentStatusRequest(Guid.NewGuid(), Guid.NewGuid(), null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGuestPaymentIntentStatusAsync_Should_NotReturn_OtherTenantsOrder_EvenWhenQueryFilterWouldAllowIt()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var context = CreateDbContext(tenantA); // global filter bound to tenant A

        var order = await SeedOrderAsync(context, tenantA);
        var created = await CreateService(context, tenantA).CreateGuestPaymentIntentAsync(
            new CreateGuestPaymentIntentRequest(order.Id, "Stripe", "Card", null, null));

        // Same context (filter still A), but the service now acts as tenant B.
        var result = await CreateService(context, tenantB).GetGuestPaymentIntentStatusAsync(
            new GetGuestPaymentIntentStatusRequest(order.Id, created.PaymentIntentId, null));

        result.Should().BeNull(
            "the explicit TenantId predicate must exclude another tenant's order/intent even when the global query filter would allow it");
    }
}
