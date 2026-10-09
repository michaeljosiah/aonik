using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Promotions;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public sealed class DeliveryReservationRecoveryTests
{
    [Fact]
    public async Task DuePreparingReplacement_Should_ReleaseUnboundHold_WhileRetainingThePreviousOrder()
    {
        var (harness, box, first) = await PendingAsync();
        var state = await harness.Checkout().GetPaymentStateAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken));
        var recovered = await harness.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId,
            CartAccessContext.ForGuest(box.CartToken, state.CartVersion));
        var access = await harness.HoldDeliveryAsync(box.Box.CartId,
            CartAccessContext.ForGuest(box.CartToken, recovered.CartVersion));
        var unavailableParties = new Mock<IPartyService>(MockBehavior.Strict);
        unavailableParties.Setup(p => p.EnsureUnverifiedGuestPartyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<CreatePartyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Party preparation unavailable."));
        var payments = new DeadlinePayments(harness);
        await using var context = harness.Commerce();
        var checkout = Checkout(harness, context, payments, unavailableParties.Object);
        await checkout.Invoking(s => s.CheckoutAsync(
                new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), access))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Party preparation unavailable.");
        var before = await context.Carts.AsNoTracking().SingleAsync();
        var preparation = CheckoutPreparation.Read(before);
        before.CheckoutState.Should().Be(CartCheckoutStates.Preparing);
        before.OrderId.Should().Be(first.OrderId);
        preparation.AttemptId.Should().NotBe(first.PaymentIntentId);
        var hold = await context.CartDeliveryReservations.AsNoTracking().SingleAsync();
        hold.PaymentAttemptId.Should().Be(preparation.AttemptId);
        hold.OrderId.Should().BeNull("replacement preparation has not yet bound its retained order");
        harness.Clock.UtcNow = preparation.ProviderStartDeadlineUtc!.Value;

        await checkout.ReconcileDeliveryReservationAsync(box.Box.CartId);

        payments.Creates.Should().BeEmpty();
        payments.Expired.Should().BeEmpty();
        payments.Reads.Should().BeEmpty();
        await using var verify = harness.Commerce();
        var cart = await verify.Carts.SingleAsync();
        cart.OrderId.Should().Be(first.OrderId);
        cart.CheckoutState.Should().Be(CartCheckoutStates.Retryable);
        cart.CheckoutDraftJson.Should().Be(before.CheckoutDraftJson);
        cart.LastActivityAtUtc.Should().Be(before.LastActivityAtUtc);
        (await verify.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.Released);
        (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(0);
        (await verify.OrderChargeSummaries.SingleAsync()).PaymentIntentId.Should().Be(first.PaymentIntentId);
    }

    [Fact]
    public async Task DueMissingPayment_Should_MaterializeTheExactFrozenDeadlineBeforeReleasingCapacityAndStock()
    {
        var (harness, box, checkout) = await PendingAsync();
        await using var context = harness.Commerce();
        var before = await context.Carts.AsNoTracking().SingleAsync();
        var preparation = CheckoutPreparation.Read(before);
        harness.Payments.States.Remove(checkout.PaymentIntentId);
        var payments = new DeadlinePayments(harness);
        harness.Clock.UtcNow = preparation.ProviderStartDeadlineUtc!.Value;

        await Checkout(harness, context, payments).ReconcileDeliveryReservationAsync(box.Box.CartId);

        var command = payments.Creates.Should().ContainSingle().Which;
        command.Should().Be(new CreateGuestPaymentIntentForOrderCommand(checkout.OrderId,
            preparation.Total, preparation.Currency, preparation.Provider, preparation.PaymentMethodType,
            preparation.ReturnUrl, preparation.CancelUrl, preparation.AttemptId,
            $"commerce:{box.Box.CartId:N}:{preparation.AttemptId:N}", preparation.ProviderStartDeadlineUtc));
        payments.Expired.Should().Equal(preparation.AttemptId);
        await using var verify = harness.Commerce();
        var cart = await verify.Carts.SingleAsync();
        cart.CheckoutState.Should().Be(CartCheckoutStates.Retryable);
        cart.CheckoutDraftJson.Should().Be(before.CheckoutDraftJson);
        cart.LastActivityAtUtc.Should().Be(before.LastActivityAtUtc, "background reconciliation is not customer activity");
        (await verify.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.Released);
        (await verify.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Released);
        (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(0);
        (await verify.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be("Cancelled");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DueUnknownPayment_Should_KeepBothCapacityAndStock_AndHideExpiredCheckoutUrl(bool providerThrows)
    {
        var (harness, box, checkout) = await PendingAsync();
        await using var context = harness.Commerce();
        var before = await context.Carts.AsNoTracking().SingleAsync();
        var preparation = CheckoutPreparation.Read(before);
        harness.Clock.UtcNow = preparation.ProviderStartDeadlineUtc!.Value.AddDays(1);
        harness.Payments.States[checkout.PaymentIntentId] = harness.Payments.States[checkout.PaymentIntentId]
            with { Status = "Processing", CheckoutUrl = "https://pay.example/old" };
        var payments = new DeadlinePayments(harness) { ThrowOnExpiry = providerThrows };
        var service = Checkout(harness, context, payments);
        var reconcile = () => service.ReconcileDeliveryReservationAsync(box.Box.CartId);

        if (providerThrows) await reconcile.Should().ThrowAsync<TimeoutException>();
        else await reconcile();
        (await harness.Inventory().ReleaseExpiredAsync()).Should().Be(0);
        var state = await service.GetPaymentStateAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken));

        state.CanEdit.Should().BeFalse();
        state.CheckoutUrl.Should().BeNull();
        payments.Creates.Should().BeEmpty();
        await using var verify = harness.Commerce();
        (await verify.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.PaymentPending);
        (await verify.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Held);
        (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(6);
        var cart = await verify.Carts.SingleAsync();
        cart.CheckoutDraftJson.Should().Be(before.CheckoutDraftJson);
        cart.LastActivityAtUtc.Should().Be(before.LastActivityAtUtc);
    }

    [Fact]
    public async Task DueCapturedPayment_Should_CommitCapacityAndStockOnce_InsteadOfReopening()
    {
        var (harness, box, checkout) = await PendingAsync();
        await using var context = harness.Commerce();
        var preparation = CheckoutPreparation.Read(await context.Carts.AsNoTracking().SingleAsync());
        harness.Clock.UtcNow = preparation.ProviderStartDeadlineUtc!.Value;
        harness.Payments.States[checkout.PaymentIntentId] = harness.Payments.States[checkout.PaymentIntentId]
            with { Status = "Captured", CheckoutUrl = null };
        var service = Checkout(harness, context, new DeadlinePayments(harness));

        await service.ReconcileDeliveryReservationAsync(box.Box.CartId);
        await service.ReconcileDeliveryReservationAsync(box.Box.CartId);

        await using var verify = harness.Commerce();
        (await verify.Carts.SingleAsync()).Status.Should().Be(CartStatuses.CheckedOut);
        (await verify.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.Committed);
        (await verify.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Committed);
        (await verify.InventoryLevels.SingleAsync()).OnHand.Should().Be(4);
        (await verify.InventoryLevels.SingleAsync()).Reserved.Should().Be(0);
        await using var ordering = harness.Ordering();
        (await ordering.Orders.SingleAsync()).Status.Should().Be("Complete");
    }

    [Fact]
    public async Task ExpiredOrdinaryHold_Should_KeepDraftAndUserActivity_WithoutCreatingPayment()
    {
        var (harness, box) = await HeldAsync();
        await using var context = harness.Commerce();
        var before = await context.Carts.AsNoTracking().SingleAsync();
        var hold = await context.CartDeliveryReservations.AsNoTracking().SingleAsync();
        harness.Clock.UtcNow = hold.ExpiresAtUtc;

        await harness.Checkout().ReconcileDeliveryReservationAsync(box.Box.CartId);

        await using var verify = harness.Commerce();
        var cart = await verify.Carts.SingleAsync();
        cart.CheckoutDraftJson.Should().Be(before.CheckoutDraftJson);
        cart.LastActivityAtUtc.Should().Be(before.LastActivityAtUtc);
        cart.Status.Should().Be(CartStatuses.Open);
        cart.OrderId.Should().BeNull();
        (await verify.CartDeliveryReservations.SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.Released);
        harness.Payments.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DueHoldWithMismatchedAttempt_Should_NotCallFinanceOrReleaseCapacity()
    {
        var (harness, box, _) = await PendingAsync();
        await using var context = harness.Commerce();
        var hold = await context.CartDeliveryReservations.SingleAsync();
        hold.PaymentAttemptId = Guid.NewGuid();
        harness.Clock.UtcNow = hold.PaymentDeadlineUtc!.Value;
        await context.SaveChangesAsync();
        var payments = new DeadlinePayments(harness);

        await Checkout(harness, context, payments).Invoking(s => s.ReconcileDeliveryReservationAsync(box.Box.CartId))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match*");

        payments.Creates.Should().BeEmpty();
        payments.Expired.Should().BeEmpty();
        (await context.CartDeliveryReservations.AsNoTracking().SingleAsync()).Status.Should().Be(DeliveryReservationStatuses.PaymentPending);
    }

    [Fact]
    public async Task DueDiscovery_Should_PagePastUnresolvedRows_AndExcludeLiveCommittedAndDeletedHolds()
    {
        var (harness, _) = await HeldAsync();
        await using var context = harness.Commerce();
        var prototype = await context.CartDeliveryReservations.SingleAsync();
        for (var index = 0; index < 55; index++)
            context.CartDeliveryReservations.Add(new CartDeliveryReservation
            {
                TenantId = harness.TenantId, CartId = Guid.NewGuid(), CapacityId = prototype.CapacityId,
                DeliveryDate = prototype.DeliveryDate, Status = DeliveryReservationStatuses.PaymentPending,
                PaymentDeadlineUtc = harness.Clock.UtcNow.AddMinutes(-1)
            });
        context.CartDeliveryReservations.Add(new CartDeliveryReservation
        {
            TenantId = harness.TenantId, CartId = Guid.NewGuid(), CapacityId = prototype.CapacityId,
            Status = DeliveryReservationStatuses.Committed, ExpiresAtUtc = harness.Clock.UtcNow.AddMinutes(-1)
        });
        context.CartDeliveryReservations.Add(new CartDeliveryReservation
        {
            TenantId = harness.TenantId, CartId = Guid.NewGuid(), CapacityId = prototype.CapacityId,
            Status = DeliveryReservationStatuses.Held, ExpiresAtUtc = harness.Clock.UtcNow.AddMinutes(-1), IsDeleted = true
        });
        await context.SaveChangesAsync();
        var service = harness.Checkout();

        var first = await service.FindDueDeliveryReservationsAsync();
        var second = await service.FindDueDeliveryReservationsAsync(first[^1].ReservationId);
        var end = await service.FindDueDeliveryReservationsAsync(second[^1].ReservationId);

        first.Should().HaveCount(50);
        second.Should().HaveCount(5);
        first.Concat(second).Select(row => row.ReservationId).Should().OnlyHaveUniqueItems();
        end.Should().BeEmpty();
    }

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box)> HeldAsync()
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("jollof");
        var created = await harness.BoxCarts().CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6));
        var full = await harness.BoxCarts().AddLineAsync(created.Box.CartId,
            new AddBoxLineCommand(fixture.DishVariants["jollof"], 6, null), CartAccessContext.ForGuest(created.CartToken, created.CartVersion));
        return (harness, await harness.HoldDeliveryAsync(full with { CartToken = created.CartToken }));
    }

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box, CheckoutResult Checkout)> PendingAsync()
    {
        var (harness, box) = await HeldAsync();
        var checkout = await harness.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));
        return (harness, box, checkout);
    }

    private static CheckoutService Checkout(BoxTestHarness harness, CommerceDbContext context, IPaymentInitiator payments,
        IPartyService? parties = null)
    {
        var tenant = new TestTenantProvider(harness.TenantId);
        return new(context, new InventoryService(context, tenant, new TenantContext { TenantId = harness.TenantId }, harness.Clock),
            new CoreOrderService(harness.Ordering(), tenant, harness.Clock, new TestCurrentUserProvider()), payments,
            new FakeBoxInvoiceWriter(), new DiscountService(context, tenant, harness.Clock), new ZeroRateTaxCalculator(),
            tenant, harness.BoxCarts(context), harness.GuestOrderAccess, new FulfilmentPromiseService(context, tenant, harness.Clock),
            new ServedTestDeliveryCoverage(), parties ?? CommerceTestHarness.Parties(), harness.Clock);
    }

    private sealed class DeadlinePayments(BoxTestHarness harness) : IPaymentInitiator
    {
        public List<CreateGuestPaymentIntentForOrderCommand> Creates { get; } = [];
        public List<Guid> Expired { get; } = [];
        public List<Guid> Reads { get; } = [];
        public bool ThrowOnExpiry { get; init; }

        public Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken cancellationToken = default)
        {
            Creates.Add(command);
            command.ProviderStartDeadlineUtc.Should().NotBeNull();
            command.ProviderStartDeadlineUtc.Should().BeOnOrBefore(harness.Clock.UtcNow);
            var id = command.PaymentIntentId!.Value;
            harness.Payments.States[id] = new(id, command.OrderId, command.Amount, command.Currency, "Cancelled", true);
            return Task.FromResult(new PaymentIntentRef(id, "Cancelled"));
        }

        public Task<PaymentIntentStateRef?> GetStateAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
        {
            Reads.Add(paymentIntentId);
            return Task.FromResult(harness.Payments.States.GetValueOrDefault(paymentIntentId));
        }

        public Task<PaymentIntentStateRef> ExpireAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
        {
            Expired.Add(paymentIntentId);
            if (ThrowOnExpiry) throw new TimeoutException();
            return Task.FromResult(harness.Payments.States[paymentIntentId]);
        }
    }
}
