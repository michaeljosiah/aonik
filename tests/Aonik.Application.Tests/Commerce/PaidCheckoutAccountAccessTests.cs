using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.IntegrationEvents;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Commerce;

public class PaidCheckoutAccountAccessTests
{
    [Fact]
    public async Task Issue_Should_UseFrozenConsentAndRecipient_AndRepeatTheSameDurableSource()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Cart.CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(
            Purchaser: new("later@example.test", "Later", "Buyer", ""), CreateAccount: false));
        await fixture.Context.SaveChangesAsync();

        await fixture.Service().IssueAsync(fixture.OrderId, fixture.PaymentId);
        await fixture.Service().IssueAsync(fixture.OrderId, fixture.PaymentId);

        var expected = new PaidAccountAccessRequest(
            fixture.TenantId, fixture.Cart.Id, fixture.OrderId, fixture.PaymentId,
            fixture.GuestPartyId, "paid@example.test");
        fixture.Requests.Should().HaveCount(2).And.OnlyContain(request => request == expected);
        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData("no-consent")]
    [InlineData("legacy")]
    [InlineData("signed-in")]
    [InlineData("open")]
    [InlineData("pending")]
    [InlineData("wrong-intent")]
    [InlineData("wrong-total")]
    [InlineData("wrong-currency")]
    [InlineData("incomplete-order")]
    [InlineData("wrong-payer")]
    [InlineData("already-owned")]
    public async Task Issue_Should_IgnoreSourcesWithoutExactCompletedGuestConsent(string state)
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        switch (state)
        {
            case "no-consent": fixture.Cart.CheckoutPreparationJson = (fixture.Preparation with { CreateAccount = false }).Serialize(); break;
            case "legacy": fixture.Cart.CheckoutPreparationJson = null; break;
            case "signed-in": fixture.Cart.CheckoutPreparationJson = (fixture.Preparation with { GuestPartyId = null }).Serialize(); break;
            case "open": fixture.Cart.Status = CartStatuses.Open; break;
            case "pending": fixture.Summary.PaymentStatus = "Pending"; break;
            case "wrong-intent": fixture.Summary.PaymentIntentId = Guid.NewGuid(); break;
            case "wrong-total": fixture.Summary.Total += 0.01m; break;
            case "wrong-currency": fixture.Summary.Currency = "USD"; break;
            case "incomplete-order": fixture.Order = fixture.Order with { Status = OrderStatusCodes.Draft }; break;
            case "wrong-payer": fixture.Order = fixture.Order with { PayerPartyId = Guid.NewGuid() }; break;
            case "already-owned": fixture.Cart.BuyerPartyId = Guid.NewGuid(); break;
        }
        // Current form state cannot manufacture consent absent from the paid preparation.
        fixture.Cart.CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(CreateAccount: true));
        await fixture.Context.SaveChangesAsync();

        await fixture.Service().IssueAsync(fixture.OrderId, fixture.PaymentId);

        fixture.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Issue_Should_NotReadAnotherTenantsPaidSource()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var otherTenantId = Guid.NewGuid();
        await using var context = new CommerceDbContext(fixture.Options, new TestTenantProvider(otherTenantId));
        var service = new PaidCheckoutAccountAccessService(context, new TestTenantProvider(otherTenantId),
            fixture.Orders.Object, fixture.Access.Object);

        await service.IssueAsync(fixture.OrderId, fixture.PaymentId);

        fixture.Requests.Should().BeEmpty();
        fixture.Orders.Verify(service => service.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifiedEvent_Should_LinkOnlyCartOwnership_AndPreserveGuestReadAndFinancialPayer()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var accountPartyId = Guid.NewGuid();
        var verified = fixture.Verified(accountPartyId);
        var anonymousToken = fixture.Cart.AnonymousToken;
        var preparation = fixture.Cart.CheckoutPreparationJson;
        var guestAccess = new GuestOrderAccess(new EphemeralDataProtectionProvider());
        var guestOrderToken = guestAccess.Issue(fixture.TenantId, fixture.OrderId);
        var handler = new CommerceAccountAccessVerifiedHandler(fixture.Service());

        await handler.HandleAsync(verified);
        await handler.HandleAsync(verified);

        var cart = await fixture.Context.Carts.AsNoTracking().SingleAsync();
        cart.BuyerPartyId.Should().Be(accountPartyId);
        cart.AnonymousToken.Should().Be(anonymousToken);
        cart.CheckoutPreparationJson.Should().Be(preparation);
        cart.LastActivityAtUtc.Should().Be(fixture.Cart.LastActivityAtUtc);
        cart.Status.Should().Be(CartStatuses.CheckedOut);
        cart.OrderId.Should().Be(fixture.OrderId);
        var summary = await fixture.Context.OrderChargeSummaries.AsNoTracking().SingleAsync();
        summary.PaymentIntentId.Should().Be(fixture.PaymentId);
        summary.PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        fixture.Order.PayerPartyId.Should().Be(fixture.GuestPartyId);
        fixture.Orders.Verify(service => service.RefreshPendingItemsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<OrderItemCommand>>(), It.IsAny<CancellationToken>()), Times.Never);

        var reads = new StorefrontOrderService(fixture.Context, new TestTenantProvider(fixture.TenantId),
            fixture.Orders.Object, guestAccess);
        (await reads.GetMyOrderAsync(accountPartyId, fixture.OrderId)).Should().NotBeNull();
        (await reads.GetMyOrderAsync(Guid.NewGuid(), fixture.OrderId)).Should().BeNull();
        (await reads.GetGuestOrderAsync(fixture.OrderId, guestOrderToken)).Should().NotBeNull();
        await fixture.Service().IssueAsync(fixture.OrderId, fixture.PaymentId);
        fixture.Requests.Should().BeEmpty("a linked account must not receive another setup request");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("cart")]
    [InlineData("order")]
    [InlineData("payment")]
    [InlineData("guest")]
    [InlineData("action")]
    [InlineData("account")]
    [InlineData("opt-out")]
    [InlineData("pending")]
    public async Task Link_Should_RejectAnUnrelatedOrUnpaidVerifiedSource(string mismatch)
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var verified = fixture.Verified(Guid.NewGuid());
        verified = mismatch switch
        {
            "tenant" => verified with { TenantId = Guid.NewGuid() },
            "cart" => verified with { CartId = Guid.NewGuid() },
            "order" => verified with { OrderId = Guid.NewGuid() },
            "payment" => verified with { PaymentIntentId = Guid.NewGuid() },
            "guest" => verified with { GuestPartyId = Guid.NewGuid() },
            "action" => verified with { ActionId = Guid.Empty },
            "account" => verified with { AccountPartyId = Guid.Empty },
            _ => verified
        };
        if (mismatch == "opt-out") fixture.Cart.CheckoutPreparationJson = (fixture.Preparation with { CreateAccount = false }).Serialize();
        if (mismatch == "pending") fixture.Summary.PaymentStatus = "Pending";
        await fixture.Context.SaveChangesAsync();

        var link = () => fixture.Service().LinkAsync(verified);

        await link.Should().ThrowAsync<InvalidOperationException>();
        (await fixture.Context.Carts.AsNoTracking().SingleAsync()).BuyerPartyId.Should().BeNull();
        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Link_Should_ResolveAConcurrentOwner_WithoutFlushingTheRejectedClaim(bool sameOwner)
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var claimant = Guid.NewGuid();
        var winner = sameOwner ? claimant : Guid.NewGuid();
        var conflict = new ConcurrentClaim(async () =>
        {
            await using var rival = new CommerceDbContext(fixture.Options, new TestTenantProvider(fixture.TenantId));
            var cart = await rival.Carts.SingleAsync();
            cart.BuyerPartyId = winner;
            await rival.SaveChangesAsync();
        });
        var options = new DbContextOptionsBuilder<CommerceDbContext>(fixture.Options).AddInterceptors(conflict).Options;
        await using var context = new CommerceDbContext(options, new TestTenantProvider(fixture.TenantId));
        var service = fixture.Service(context);

        var link = () => service.LinkAsync(fixture.Verified(claimant));

        if (sameOwner) await link();
        else await link.Should().ThrowAsync<InvalidOperationException>().WithMessage("*another account*");
        context.ChangeTracker.HasChanges().Should().BeFalse();
        await context.SaveChangesAsync();
        (await context.Carts.AsNoTracking().SingleAsync()).BuyerPartyId.Should().Be(winner);
        fixture.Order.PayerPartyId.Should().Be(fixture.GuestPartyId);
    }

    private sealed class ConcurrentClaim(Func<Task> rival) : SaveChangesInterceptor
    {
        private bool _claimed;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_claimed)
            {
                _claimed = true;
                await rival();
                // InMemory has no native rowversion; exercise the production SQL conflict path.
                throw new DbUpdateConcurrencyException("A verified claim won the cart rowversion.");
            }
            return result;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();
        public Guid PaymentId { get; } = Guid.NewGuid();
        public Guid GuestPartyId { get; } = Guid.NewGuid();
        public DbContextOptions<CommerceDbContext> Options { get; } = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options;
        public CommerceDbContext Context { get; }
        public Mock<IOrderService> Orders { get; } = new();
        public Mock<IPaidAccountAccessService> Access { get; } = new();
        public List<PaidAccountAccessRequest> Requests { get; } = [];
        public Cart Cart { get; }
        public OrderChargeSummary Summary { get; }
        public OrderDto Order { get; set; }
        public CheckoutPreparation Preparation { get; }

        public Fixture()
        {
            Context = new CommerceDbContext(Options, new TestTenantProvider(TenantId));
            var delivery = BoxTestHarness.ValidDelivery;
            Preparation = new CheckoutPreparation(PaymentId, GuestPartyId, "GBP", "Stripe", "Card", null, null, null,
                95, 0, null, null, 0, 95, [], [], [], [], new OrderDeliveryDto(
                    delivery.Purchaser with { Email = "paid@example.test" }, delivery.Address, delivery.DeliveryDate,
                    "Europe/London", new DeliveryRecipientDto("Recipient", "07000000000")), CreateAccount: true);
            Cart = new Cart { TenantId = TenantId, OrderId = OrderId, Currency = "GBP", Status = CartStatuses.CheckedOut,
                AnonymousToken = CartAccess.MintToken(), CheckoutPreparationJson = Preparation.Serialize(),
                LastActivityAtUtc = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc) };
            Summary = new OrderChargeSummary { TenantId = TenantId, OrderId = OrderId, PaymentIntentId = PaymentId,
                Currency = "GBP", Subtotal = 95, Total = 95, PaymentStatus = CheckoutPaymentStatuses.Captured };
            Order = new OrderDto(OrderId, TenantId, OrderTypeCodes.ProductPurchase, OrderStatusCodes.Complete,
                GuestPartyId, 95, "GBP", DateTime.UtcNow, []);
            Orders.Setup(service => service.GetAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(() => Order);
            Access.Setup(service => service.IssueAsync(It.IsAny<PaidAccountAccessRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PaidAccountAccessRequest, CancellationToken>((request, _) => Requests.Add(request))
                .Returns(Task.CompletedTask);
        }

        public async Task SeedAsync()
        {
            Context.Carts.Add(Cart);
            Context.OrderChargeSummaries.Add(Summary);
            await Context.SaveChangesAsync();
        }

        public PaidCheckoutAccountAccessService Service(CommerceDbContext? context = null) => new(
            context ?? Context, new TestTenantProvider(TenantId), Orders.Object, Access.Object);

        public AccountAccessVerifiedEvent Verified(Guid accountPartyId) => new(
            TenantId, Guid.NewGuid(), Cart.Id, OrderId, PaymentId, GuestPartyId, accountPartyId);

        public void Dispose() => Context.Dispose();
    }
}
