using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public sealed class GiftCheckoutTests
{
    private const string CardConfiguration = "Commerce.Storefront.GreetingCard";
    private static CheckoutDeliveryDetails Delivery => BoxTestHarness.ValidDelivery with
    {
        Recipient = new("Gift Recipient", "+44 7700 900456")
    };

    [Fact]
    public async Task Checkout_Should_FreezeOneCardAndGiftSnapshot_AndChargeIdenticalOrderInvoiceAndPaymentTotals()
    {
        var (h, box, access) = await ArrangeAsync();
        var extra = await h.AddExtraAsync("extra", 8m);
        await h.BoxCarts().AddExtraLineAsync(box.Box.CartId, new(extra.VariantId, 1), access);
        h.Settings[CommerceSettingNames.StorefrontDeliveryChargedAmount] = "7";
        await using (var db = h.Commerce())
        {
            db.Discounts.Add(new Discount { TenantId = h.TenantId, Code = "TEN", Kind = DiscountKinds.FixedAmount,
                Value = 10m, Currency = "GBP", IsActive = true });
            await db.SaveChangesAsync();
        }
        var quote = await h.Carts().ApplyDiscountAsync(box.Box.CartId, "TEN", access);
        var command = new CheckoutCommand(box.Box.CartId, "Stripe", "Card", CustomerAccountId: Guid.NewGuid(),
            Delivery: Delivery, ExpectedTotal: quote.Total);

        var result = await h.Checkout().CheckoutAsync(command, access);

        result.Total.Should().Be(103m); // 95 box + 8 extra + 3 card - 10 discount + 7 delivery.
        h.Payments.LastAmount.Should().Be(result.Total);
        h.Invoices.LastCommand!.Lines.Sum(x => x.Quantity * x.UnitPrice).Should().Be(result.Total);
        h.Invoices.LastCommand.Lines.Should().ContainSingle(x => x.Description == "Greeting card" && x.UnitPrice == 3m);
        await using var ordering = h.Ordering();
        var items = (await ordering.Orders.Include(x => x.Items).SingleAsync()).Items;
        var card = items.Single(x => x.ItemType == CheckoutService.GreetingCardItemType);
        card.ProductId.Should().BeNull(); card.Quantity.Should().Be(1); card.AmountIn.Should().Be(3);
        items.Select(x => x.ItemIndex).Should().OnlyHaveUniqueItems();
        await using var commerce = h.Commerce();
        var summary = await commerce.OrderChargeSummaries.SingleAsync();
        summary.Subtotal.Should().Be(106); summary.GreetingCardCharged.Should().Be(3);
        summary.Total.Should().Be(result.Total);
        var gift = await commerce.OrderDeliveryDetails.SingleAsync();
        gift.IsGift.Should().BeTrue(); gift.HidePrices.Should().BeTrue(); gift.IncludeGreetingCard.Should().BeTrue();
        gift.GreetingCardMessage.Should().Be("Happy birthday!\nFrom <Pat>");
        gift.RecipientName.Should().Be("Gift Recipient");
        (await commerce.OrderBundleSelections.SumAsync(x => x.Quantity)).Should().Be(6);
        (await commerce.InventoryReservations.SumAsync(x => x.Quantity)).Should().Be(7, "the greeting card is not catalog inventory");

        h.Settings[CardConfiguration] = "invalid after payment preparation";
        var frozen = await h.BoxCarts().GetAsync(box.Box.CartId, access);
        frozen.Quote.Total.Should().Be(result.Total);
        frozen.Quote.Components.Single(x => x.Key == "boxPrice").Amount.Should().Be(95);
        frozen.Quote.Components.Single(x => x.Key == "greetingCard").Amount.Should().Be(3);
        frozen.Quote.Components.Single(x => x.Key == "deliveryCharged").Amount.Should().Be(7);
        var replay = await h.Checkout().CheckoutAsync(command with { ExpectedTotal = 1 }, access);
        replay.OrderId.Should().Be(result.OrderId); replay.PaymentIntentId.Should().Be(result.PaymentIntentId);
        h.Payments.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(98)]
    public async Task SelectedCard_Should_RequireAcceptanceOfCurrentTotal_BeforeAnyPayment(int? expected)
    {
        var (h, box, access) = await ArrangeAsync();
        h.Settings[CardConfiguration] = """{"isEnabled":true,"currency":"GBP","amount":4}""";
        var act = () => h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card",
            Delivery: Delivery, ExpectedTotal: expected), access);
        (await act.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.PriceChanged);
        h.Payments.Calls.Should().Be(0);
        await using var db = h.Commerce();
        (await db.OrderChargeSummaries.AnyAsync()).Should().BeFalse();
        (await db.InventoryReservations.AnyAsync()).Should().BeFalse();
        var accepted = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card",
            Delivery: Delivery, ExpectedTotal: 99), access);
        accepted.Total.Should().Be(99);
    }

    [Fact]
    public async Task GiftWithoutCard_Should_NotRequirePriceConfiguration_AndPreserveExplicitVisiblePrices()
    {
        var (h, box, access) = await ArrangeAsync(card: false, hidePrices: false);
        h.Settings.Clear();
        var result = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: Delivery), access);
        result.Total.Should().Be(95);
        await using var db = h.Commerce();
        var gift = await db.OrderDeliveryDetails.SingleAsync();
        gift.IsGift.Should().BeTrue(); gift.HidePrices.Should().BeFalse();
        gift.IncludeGreetingCard.Should().BeFalse(); gift.GreetingCardMessage.Should().BeNull();
        (await db.OrderChargeSummaries.SingleAsync()).GreetingCardCharged.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmedUnpaidRecovery_Should_RemoveGiftWithoutChangingFood_AndReplaceItsFrozenFee()
    {
        var (h, box, access) = await ArrangeAsync();
        var first = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: Delivery, ExpectedTotal: 98), access);
        var recovery = await h.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId, access);
        recovery.CanEdit.Should().BeTrue();
        var draft = await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, new(Gift: new(GiftIntent: false)),
            access with { ExpectedCartVersion = recovery.CartVersion });
        var nextAccess = await h.HoldDeliveryAsync(box.Box.CartId, access with { ExpectedCartVersion = draft.CartVersion });
        var second = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: Delivery), nextAccess);
        second.OrderId.Should().Be(first.OrderId); second.PaymentIntentId.Should().NotBe(first.PaymentIntentId);
        second.Total.Should().Be(95);
        (await h.Checkout().ConfirmPaymentAsync(first.OrderId, first.PaymentIntentId, first.Total, first.Currency)).Should().BeFalse();
        await using var db = h.Commerce();
        (await db.OrderChargeSummaries.SingleAsync()).GreetingCardCharged.Should().Be(0);
        var snapshot = await db.OrderDeliveryDetails.SingleAsync();
        snapshot.IsGift.Should().BeFalse(); snapshot.GreetingCardMessage.Should().BeNull();
        (await db.OrderBundleSelections.SumAsync(x => x.Quantity)).Should().Be(6);
        await using var ordering = h.Ordering();
        (await ordering.Orders.Include(x => x.Items).SingleAsync()).Items.Should().NotContain(x => x.ItemType == CheckoutService.GreetingCardItemType);
    }

    [Fact]
    public async Task ExactPaymentCompletion_Should_KeepGiftSnapshot_AndNotConsumeFoodTwice()
    {
        var (h, box, access) = await ArrangeAsync();
        var result = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: Delivery, ExpectedTotal: 98), access);
        var wrongAmount = () => h.Checkout().ConfirmPaymentAsync(result.OrderId, result.PaymentIntentId, 95, "GBP");
        await wrongAmount.Should().ThrowAsync<InvalidOperationException>().WithMessage("Payment amount or currency*");
        h.Payments.States[result.PaymentIntentId] = h.Payments.States[result.PaymentIntentId] with { Status = "Captured" };
        (await h.Checkout().ConfirmPaymentAsync(result.OrderId, result.PaymentIntentId, result.Total, result.Currency)).Should().BeTrue();
        (await h.Checkout().ConfirmPaymentAsync(result.OrderId, result.PaymentIntentId, result.Total, result.Currency)).Should().BeTrue();
        await using var db = h.Commerce();
        (await db.InventoryLevels.SingleAsync()).OnHand.Should().Be(4);
        (await db.OrderDeliveryDetails.SingleAsync()).GreetingCardMessage.Should().Be("Happy birthday!\nFrom <Pat>");
        (await db.OrderChargeSummaries.SingleAsync()).GreetingCardCharged.Should().Be(3);
    }

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box, CartAccessContext Access)> ArrangeAsync(
        bool card = true, bool hidePrices = true)
    {
        var h = new BoxTestHarness();
        h.Settings[CardConfiguration] = """{"isEnabled":true,"currency":"GBP","amount":3}""";
        var fixture = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6, new(fixture.DishVariants["dish"], 6)));
        box = await h.HoldDeliveryAsync(box);
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);
        var draft = await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId,
            new(DeliveryDate: Delivery.DeliveryDate, Gift: new(true, hidePrices, card, "Happy birthday!\nFrom <Pat>")), access);
        return (h, box, access with { ExpectedCartVersion = draft.CartVersion });
    }
}
