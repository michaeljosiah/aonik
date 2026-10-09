using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public sealed class CheckoutRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recover_Should_RefreshIdleWindowOnlyAfterConfirmedRecovery(bool confirmedClosure)
    {
        var (harness, box, checkout) = await PendingAsync();
        DateTime? previousActivity;
        await using (var before = harness.Commerce())
            previousActivity = (await before.Carts.SingleAsync()).LastActivityAtUtc;
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddDays(30);
        if (!confirmedClosure)
            harness.Payments.States[checkout.PaymentIntentId] = harness.Payments.States[checkout.PaymentIntentId]
                with { Status = "Processing", CheckoutUrl = null };

        var recovery = await harness.Checkout().RecoverAsync(box.Box.CartId, checkout.PaymentIntentId,
            await CurrentAccessAsync(harness, box));
        var abandoned = await harness.Maintenance().AbandonIdleBoxCartsAsync();

        recovery.CanEdit.Should().Be(confirmedClosure);
        abandoned.Should().Be(0);
        await using var after = harness.Commerce();
        var cart = await after.Carts.SingleAsync();
        cart.Status.Should().Be(CartStatuses.Open);
        cart.LastActivityAtUtc.Should().Be(confirmedClosure ? harness.Clock.UtcNow : previousActivity);
    }

    [Fact]
    public async Task Recover_Should_ReopenSameCartAndReuseOrder_When_ProviderConfirmsUnpaidClosure()
    {
        var (harness, box, first) = await PendingAsync();
        var access = await CurrentAccessAsync(harness, box);

        var cancelled = await harness.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId, access);

        cancelled.Status.Should().Be("cancelled");
        cancelled.CanEdit.Should().BeTrue();
        cancelled.OrderId.Should().Be(first.OrderId);
        cancelled.CheckoutUrl.Should().BeNull();
        (await harness.Inventory().GetAvailableAsync(box.Box.Lines.Single().VariantId)).Should().Be(10m);
        var revisedDelivery = BoxTestHarness.ValidDelivery with
        {
            Address = BoxTestHarness.ValidDelivery.Address with { Line1 = "2 Revised Street" }
        };
        var second = await harness.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card", Delivery: revisedDelivery),
            CartAccessContext.ForGuest(box.CartToken, cancelled.CartVersion));

        second.OrderId.Should().Be(first.OrderId);
        second.PaymentIntentId.Should().NotBe(first.PaymentIntentId);
        second.Total.Should().Be(first.Total);
        harness.Payments.Calls.Should().Be(2);
        harness.Payments.States[first.PaymentIntentId].CanNoLongerPay.Should().BeTrue();
        harness.Payments.States[second.PaymentIntentId].CanNoLongerPay.Should().BeFalse();
        await using var commerce = harness.Commerce();
        (await commerce.Carts.SingleAsync()).OrderId.Should().Be(first.OrderId);
        (await commerce.Carts.SingleAsync()).CheckoutState.Should().Be(CartCheckoutStates.AwaitingPayment);
        (await commerce.OrderChargeSummaries.SingleAsync()).PaymentIntentId.Should().Be(second.PaymentIntentId);
        (await commerce.OrderDeliveryDetails.SingleAsync()).AddressLine1.Should().Be("2 Revised Street");
        (await commerce.OrderBundleSelections.CountAsync()).Should().Be(1);
        (await commerce.InventoryReservations.Where(r => r.Status == InventoryReservationStatuses.Held)
            .SumAsync(r => r.Quantity)).Should().Be(6m);
        (await commerce.InventoryLevels.SingleAsync()).Reserved.Should().Be(6m);
        await using var ordering = harness.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(1);
        (await ordering.Orders.Include(o => o.Items).SingleAsync()).Items.Should().ContainSingle();

        var staleCompletion = await harness.Checkout().ConfirmPaymentAsync(first.OrderId, first.PaymentIntentId, first.Total, first.Currency);
        staleCompletion.Should().BeFalse("the retired attempt cannot complete its replacement");
    }

    [Theory]
    [InlineData("attempt")]
    [InlineData("version")]
    [InlineData("missing-version")]
    public async Task Recover_Should_RejectStaleProof_BeforeProviderClosureOrStockRelease(string stale)
    {
        var (harness, box, checkout) = await PendingAsync();
        var access = await CurrentAccessAsync(harness, box);
        var attempt = stale == "attempt" ? Guid.NewGuid() : checkout.PaymentIntentId;
        if (stale == "version") access = CartAccessContext.ForGuest(box.CartToken, Convert.ToBase64String(new byte[8]));
        if (stale == "missing-version") access = CartAccessContext.ForGuest(box.CartToken);

        var recover = () => harness.Checkout().RecoverAsync(box.Box.CartId, attempt, access);

        await recover.Should().ThrowAsync<CartWriteConflictException>();
        harness.Payments.States[checkout.PaymentIntentId].Status.Should().Be("Pending");
        await AssertHeldAsync(harness);
    }

    [Fact]
    public async Task Recover_Should_RejectOldAttempt_AfterANewAttemptStarts()
    {
        var (harness, box, first) = await PendingAsync();
        var cancelled = await harness.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId,
            await CurrentAccessAsync(harness, box));
        var second = await harness.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery),
            CartAccessContext.ForGuest(box.CartToken, cancelled.CartVersion));

        var recover = () => harness.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId,
            CartAccessContext.ForGuest(box.CartToken, cancelled.CartVersion));

        await recover.Should().ThrowAsync<CartWriteConflictException>();
        harness.Payments.States[second.PaymentIntentId].Status.Should().Be("Pending");
        await AssertHeldAsync(harness);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverAndMaintenance_Should_PreserveUnknownPaymentAndExpiredStockHolds(bool missingState)
    {
        var (harness, box, checkout) = await PendingAsync();
        if (missingState) harness.Payments.States.Remove(checkout.PaymentIntentId);
        else harness.Payments.States[checkout.PaymentIntentId] = harness.Payments.States[checkout.PaymentIntentId]
            with { Status = "Processing", CheckoutUrl = null };

        var recovery = await harness.Checkout().RecoverAsync(box.Box.CartId, checkout.PaymentIntentId,
            await CurrentAccessAsync(harness, box));
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddDays(30);
        var released = await harness.Inventory().ReleaseExpiredAsync();
        var abandoned = await harness.Maintenance().AbandonIdleBoxCartsAsync();

        recovery.Status.Should().Be("processing");
        recovery.CanEdit.Should().BeFalse();
        recovery.CheckoutUrl.Should().BeNull();
        released.Should().Be(0);
        abandoned.Should().Be(0);
        harness.Payments.Calls.Should().Be(1);
        await AssertHeldAsync(harness);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    public async Task ConfirmPayment_Should_RejectDifferentMoney_WithoutCommittingStockOrCompletingOrder(string mismatch)
    {
        var (harness, _, checkout) = await PendingAsync();

        var confirm = () => harness.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId,
            mismatch == "amount" ? checkout.Total + 0.01m : checkout.Total,
            mismatch == "currency" ? "USD" : checkout.Currency);

        await confirm.Should().ThrowAsync<InvalidOperationException>().WithMessage("*amount or currency*");
        await AssertHeldAsync(harness);
        await using var ordering = harness.Ordering();
        (await ordering.Orders.SingleAsync()).Status.Should().NotBe("Complete");
        await using var commerce = harness.Commerce();
        (await commerce.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task Recover_Should_ConvergeCaptureInsteadOfReopening_AndCompletionReplayMustNotConsumeStockTwice()
    {
        var (harness, box, checkout) = await PendingAsync();
        harness.Payments.States[checkout.PaymentIntentId] = harness.Payments.States[checkout.PaymentIntentId]
            with { Status = "Captured", CheckoutUrl = null };

        var result = await harness.Checkout().RecoverAsync(box.Box.CartId, checkout.PaymentIntentId,
            await CurrentAccessAsync(harness, box));
        var duplicate = await harness.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId,
            checkout.Total, checkout.Currency);

        result.Status.Should().Be("succeeded");
        result.CanEdit.Should().BeFalse();
        result.CheckoutUrl.Should().BeNull();
        duplicate.Should().BeTrue("a matching duplicate may retry downstream confirmation delivery");
        await using var commerce = harness.Commerce();
        (await commerce.Carts.SingleAsync()).Status.Should().Be(CartStatuses.CheckedOut);
        (await commerce.OrderChargeSummaries.SingleAsync()).PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        (await commerce.InventoryReservations.SingleAsync()).Status.Should().Be(InventoryReservationStatuses.Committed);
        var stock = await commerce.InventoryLevels.SingleAsync();
        stock.OnHand.Should().Be(4m);
        stock.Reserved.Should().Be(0m);
        await using var ordering = harness.Ordering();
        (await ordering.Orders.SingleAsync()).Status.Should().Be("Complete");
    }

    [Fact]
    public async Task Recover_Should_NotRevealOrChangeAnotherGuestsCheckout()
    {
        var (harness, box, checkout) = await PendingAsync();

        var recover = () => harness.Checkout().RecoverAsync(box.Box.CartId, checkout.PaymentIntentId,
            CartAccessContext.ForGuest("wrong-token", box.CartVersion));

        await recover.Should().ThrowAsync<NotFoundException>();
        harness.Payments.States[checkout.PaymentIntentId].Status.Should().Be("Pending");
        await AssertHeldAsync(harness);
    }

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box, CheckoutResult Checkout)> PendingAsync()
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("jollof");
        var created = await harness.BoxCarts().CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6));
        var full = await harness.BoxCarts().AddLineAsync(created.Box.CartId,
            new AddBoxLineCommand(fixture.DishVariants["jollof"], 6, null),
            CartAccessContext.ForGuest(created.CartToken, created.CartVersion));
        var box = full with { CartToken = created.CartToken };
        var checkout = await harness.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));
        return (harness, box, checkout);
    }

    private static async Task<CartAccessContext> CurrentAccessAsync(BoxTestHarness harness, BoxCartDto box)
    {
        var state = await harness.Checkout().GetPaymentStateAsync(box.Box.CartId, CartAccessContext.ForGuest(box.CartToken));
        return CartAccessContext.ForGuest(box.CartToken, state.CartVersion);
    }

    private static async Task AssertHeldAsync(BoxTestHarness harness)
    {
        await using var commerce = harness.Commerce();
        var cart = await commerce.Carts.SingleAsync();
        cart.Status.Should().Be(CartStatuses.Open);
        cart.CheckoutState.Should().Be(CartCheckoutStates.AwaitingPayment);
        (await commerce.InventoryReservations.Where(r => r.Status == InventoryReservationStatuses.Held).ToListAsync())
            .Should().ContainSingle().Which.Quantity.Should().Be(6m);
        var stock = await commerce.InventoryLevels.SingleAsync();
        stock.OnHand.Should().Be(10m);
        stock.Reserved.Should().Be(6m);
    }
}
