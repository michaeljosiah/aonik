using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Observability;
using Aonik.Finance.Services.Payments;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Payments;

public class PaymentServiceTests
{
    private class TestTenantProvider : ITenantProvider
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

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId) => _userId = userId;

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static PaymentService CreateService(FinanceDbContext context, Guid tenantId) =>
        new(
            context,
            new TestTenantProvider(tenantId),
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);

    // A payment intent always funds an order, and the service now resolves the payer from
    // that order, so tests must seed a real order to create an intent against.
    private static async Task<Order> SeedOrderAsync(
        FinanceDbContext context,
        Guid tenantId,
        Guid? payerPartyId = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = "BillPayment",
            PayerPartyId = payerPartyId,
            AmountIn = 100m,
            CurrencyIn = "USD",
            Status = "Draft",
            FeesJson = "[]",
            ProvenanceJson = "{}"
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_ShouldCreatePaymentIntent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);
        var payerPartyId = Guid.NewGuid();
        var order = await SeedOrderAsync(context, tenantId, payerPartyId);
        var request = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-001", order.Id, null);

        // Act
        var result = await service.CreatePaymentIntentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.OrderId.Should().Be(order.Id);
        result.Amount.Should().Be(100.00m);
        result.Currency.Should().Be("USD");
        result.Reference.Should().Be("ORDER-001");
        result.Status.Should().Be(PaymentStatus.Pending);
        result.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        var savedPayment = await context.PaymentIntents.FirstOrDefaultAsync(p => p.Id == result.Id);
        savedPayment.Should().NotBeNull();
        savedPayment!.Amount.Should().Be(100.00m);
        savedPayment.PayerPartyId.Should().Be(payerPartyId); // resolved from the order, not Guid.Empty
        savedPayment.PaymentMethodType.Should().BeNull();     // no silent "Card" default
    }

    [Fact]
    public async Task GetPaymentIntentAsync_Should_NotReturn_OtherTenantsIntent_EvenWhenQueryFilterWouldAllowIt()
    {
        // M7 defense-in-depth: the explicit `&& TenantId == tenantId` predicate on
        // sensitive payment lookups must isolate the row independently of the global
        // query filter. We seed the intent under tenant A (so the DbContext's filter —
        // bound to A — WOULD allow the row), then read it back through a service whose
        // current tenant is B. Without the explicit predicate this leaks tenant A's
        // intent whenever the filter tenant != the request tenant; with it, the lookup
        // is empty. (This fails if the predicate is removed and only the filter guards.)
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var context = CreateDbContext(tenantA); // global filter bound to tenant A

        var order = await SeedOrderAsync(context, tenantA);
        var intent = await CreateService(context, tenantA)
            .CreatePaymentIntentAsync(new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-001", order.Id, null));

        // Same context (filter still A), but the service now acts as tenant B.
        var result = await CreateService(context, tenantB).GetPaymentIntentAsync(intent.Id);

        result.Should().BeNull(
            "the explicit TenantId predicate must exclude another tenant's intent even when the global query filter would allow it");
    }

    [Fact]
    public async Task GetPaymentIntentAsync_ShouldReturnPaymentIntent_WhenExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);
        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var createRequest = new CreatePaymentIntentRequest(250.00m, "EUR", "ORDER-002", order.Id, null);
        var created = await service.CreatePaymentIntentAsync(createRequest);

        // Act
        var result = await service.GetPaymentIntentAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.OrderId.Should().Be(order.Id);
        result.Amount.Should().Be(250.00m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task GetPaymentIntentAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);

        // Act
        var result = await service.GetPaymentIntentAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CapturePaymentAsync_ShouldCapturePayment_WhenAuthorized()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);
        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-003", order.Id, null, PaymentMethodType: "Card");
        var created = await service.CreatePaymentIntentAsync(createRequest);

        // Authorize the payment first (set status directly since entities are anemic)
        var payment = await context.PaymentIntents.FirstAsync(p => p.Id == created.Id);
        payment.Status = "Authorized";
        await context.SaveChangesAsync();

        // Capture now posts to the ledger (Dr Cash / Cr Payments Clearing), so the
        // tenant needs a ledger and a Cash (1000) account. The poster materialises
        // the Payments Clearing (2100) account on demand, so it is not seeded here.
        var ledgerId = Guid.NewGuid();
        context.Ledgers.Add(new LedgerEntity
        {
            Id = ledgerId,
            TenantId = tenantId,
            BaseCurrency = "USD",
            CreatedAt = DateTime.UtcNow
        });
        context.LedgerAccounts.Add(new LedgerAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledgerId,
            AccountType = "Asset",
            Name = "Cash",
            Code = "1000",
            DimensionsJson = "{}",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.CapturePaymentAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PaymentStatus.Captured);

        // Capture publishes PaymentCompletedEvent via the transactional outbox so downstream modules
        // (e.g. Aonik.Commerce) can complete checkout. It is the single producer of that event.
        var completedEvents = await context.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>()
            .Where(m => m.EventType.EndsWith("PaymentCompletedEvent"))
            .ToListAsync();
        completedEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task CapturePaymentAsync_ShouldThrow_WhenPaymentNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);

        // Act
        var act = async () => await service.CapturePaymentAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Payment intent with ID * not found");
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldCancelPayment_WhenPending()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);
        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var createRequest = new CreatePaymentIntentRequest(100.00m, "USD", "ORDER-004", order.Id, null);
        var created = await service.CreatePaymentIntentAsync(createRequest);

        // Act
        var result = await service.CancelPaymentAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task CancelPaymentAsync_ShouldThrow_WhenPaymentNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var service = new PaymentService(
            context,
            tenantProvider,
            new AllowAllPermissionService(),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new FinanceMetrics(),
            new LedgerPostingService(context),
            NullLogger<PaymentService>.Instance);

        // Act
        var act = async () => await service.CancelPaymentAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Payment intent with ID * not found");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Should_PreferExplicitPayerOverride_When_Supplied()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var orderPayer = Guid.NewGuid();
        var overridePayer = Guid.NewGuid();
        var order = await SeedOrderAsync(context, tenantId, orderPayer);
        var request = new CreatePaymentIntentRequest(
            100m, "USD", "ORDER-OVR", order.Id, null, PayerPartyId: overridePayer);

        // Act
        var result = await service.CreatePaymentIntentAsync(request);

        // Assert
        var saved = await context.PaymentIntents.FirstAsync(p => p.Id == result.Id);
        saved.PayerPartyId.Should().Be(overridePayer); // explicit override wins over the order's payer
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Should_PersistNullPayer_When_OrderHasNoPayer()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var order = await SeedOrderAsync(context, tenantId, payerPartyId: null);
        var request = new CreatePaymentIntentRequest(100m, "USD", "ORDER-NP", order.Id, null);

        // Act
        var result = await service.CreatePaymentIntentAsync(request);

        // Assert
        var saved = await context.PaymentIntents.FirstAsync(p => p.Id == result.Id);
        saved.PayerPartyId.Should().BeNull(); // genuine absence, never the Guid.Empty placeholder
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Should_PersistMethod_When_Supplied()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var request = new CreatePaymentIntentRequest(
            100m, "USD", "ORDER-PM", order.Id, null, PaymentMethodType: "BankTransfer");

        // Act
        var result = await service.CreatePaymentIntentAsync(request);

        // Assert
        var saved = await context.PaymentIntents.FirstAsync(p => p.Id == result.Id);
        saved.PaymentMethodType.Should().Be("BankTransfer");
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_Should_Throw_When_OrderNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var request = new CreatePaymentIntentRequest(100m, "USD", "ORDER-MISSING", Guid.NewGuid(), null);

        // Act
        var act = async () => await service.CreatePaymentIntentAsync(request);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Order with ID * not found");
    }

    [Fact]
    public async Task AuthorizePaymentAsync_Should_Throw_When_PayerUnresolved()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        // Order has no payer; a method is supplied so only the payer guard can trip.
        var order = await SeedOrderAsync(context, tenantId, payerPartyId: null);
        var created = await service.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(100m, "USD", "ORDER-NOPAYER", order.Id, null, PaymentMethodType: "Card"));

        // Act
        var act = async () => await service.AuthorizePaymentAsync(created.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*no resolved payer*");
    }

    [Fact]
    public async Task AuthorizePaymentAsync_Should_Throw_When_MethodMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        // Order has a payer, but no method was supplied at creation.
        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var created = await service.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(100m, "USD", "ORDER-NOMETHOD", order.Id, null));

        // Act
        var act = async () => await service.AuthorizePaymentAsync(created.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*no payment method*");
    }

    [Fact]
    public async Task AuthorizePaymentAsync_Should_Authorize_When_PayerAndMethodPresent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var created = await service.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(100m, "USD", "ORDER-OK", order.Id, null, PaymentMethodType: "Card"));

        // Act
        var result = await service.AuthorizePaymentAsync(created.Id);

        // Assert
        result.Status.Should().Be(PaymentStatus.Authorized);
    }

    [Fact]
    public async Task CapturePaymentAsync_Should_FailClosed_When_AuthorizedIntentHasEmptyPayer()
    {
        // Arrange — a legacy intent already in Authorized status (created before this invariant
        // existed) with the Guid.Empty placeholder payer, reached without AuthorizePaymentAsync.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var created = await service.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(100m, "USD", "ORDER-LEGACY-P", order.Id, null, PaymentMethodType: "Card"));

        var intent = await context.PaymentIntents.FirstAsync(p => p.Id == created.Id);
        intent.PayerPartyId = Guid.Empty;
        intent.Status = "Authorized";
        await context.SaveChangesAsync();

        // Act
        var act = async () => await service.CapturePaymentAsync(created.Id);

        // Assert — capture is blocked at the ledger boundary; no money moved (status unchanged).
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*no resolved payer*");
        var after = await context.PaymentIntents.AsNoTracking().FirstAsync(p => p.Id == created.Id);
        after.Status.Should().Be("Authorized");
    }

    [Fact]
    public async Task CapturePaymentAsync_Should_FailClosed_When_AuthorizedIntentHasNoMethod()
    {
        // Arrange — a legacy intent authorized despite a blank rail.
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId);

        var order = await SeedOrderAsync(context, tenantId, Guid.NewGuid());
        var created = await service.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest(100m, "USD", "ORDER-LEGACY-M", order.Id, null)); // no method

        var intent = await context.PaymentIntents.FirstAsync(p => p.Id == created.Id);
        intent.Status = "Authorized";
        await context.SaveChangesAsync();

        // Act
        var act = async () => await service.CapturePaymentAsync(created.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*no payment method*");
        var after = await context.PaymentIntents.AsNoTracking().FirstAsync(p => p.Id == created.Id);
        after.Status.Should().Be("Authorized");
    }
}
