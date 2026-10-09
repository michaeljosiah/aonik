using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.GiftCards;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Payments;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

using static CartTestAccess;

public partial class CheckoutServiceTests
{
    [Theory]
    [InlineData("Email", 50, 0)]
    [InlineData("Post", 55.50, .50)]
    public async Task GuestGiftPurchase_Should_FreezeDeliveryAndRealOrderItemBeforePayment_WithoutFoodDeliveryOrStock(
        string method, decimal expectedTotal, decimal expectedTax)
    {
        var h = new Harness { GiftCardsEnabled = true, Tax = new TenPercentTax() };
        var cart = await CreateGiftPurchaseCartAsync(h, method);
        var access = CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion);
        var calls = 0;
        h.Payments.BeforeCreate = async command =>
        {
            calls++;
            await using var commerce = h.Commerce();
            await using var ordering = h.Ordering();
            var item = await ordering.OrderItems.SingleAsync(i => i.ItemType == CartLineKinds.GiftCardValue);
            var purchase = command.GiftCard!.Purchase!;
            purchase.OrderItemId.Should().Be(item.Id);
            purchase.FaceValue.Should().Be(50m);
            command.GiftCard.CartId.Should().Be(cart.Id);
            command.Amount.Should().Be(expectedTotal);
            var delivery = await commerce.OrderGiftCardDeliveries.SingleAsync();
            delivery.OrderId.Should().Be(command.OrderId);
            delivery.OrderItemId.Should().Be(item.Id);
            delivery.PaymentIntentId.Should().Be(command.PaymentIntentId!.Value);
            delivery.DeliveryMethod.Should().Be(method);
            delivery.Status.Should().Be("PendingIssuance");
            var snapshot = GiftCardDeliveryData.Read(delivery);
            snapshot.RecipientName.Should().Be("Recipient Person");
            snapshot.OrderItemId.Should().Be(item.Id);
            snapshot.PostalAddress.Should().Be(method == "Post" ? GiftPostalAddress : null);
            (await commerce.OrderDeliveryDetails.CountAsync()).Should().Be(0);
            (await commerce.CartDeliveryReservations.CountAsync()).Should().Be(0);
            (await commerce.InventoryReservations.CountAsync()).Should().Be(0);
            (await ordering.Orders.SingleAsync()).PayerPartyId.Should().NotBeNull();
        };

        var result = await h.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card", ExpectedTotal: expectedTotal), access);

        calls.Should().Be(1);
        result.Total.Should().Be(expectedTotal);
        result.TaxTotal.Should().Be(expectedTax);
        result.GuestOrderToken.Should().NotBeNullOrWhiteSpace();
        await using var verify = h.Commerce();
        (await verify.Carts.SingleAsync()).BuyerPartyId.Should().BeNull();
    }

    [Theory]
    [InlineData(30, null, 70)]
    [InlineData(30, 100, null)]
    [InlineData(30, 100, 71)]
    [InlineData(100, 100, null)]
    [InlineData(100, 100, 1)]
    public async Task GiftTender_Should_RejectMissingOrStaleAcceptedCashBeforeCreatingOrderOrAttempt(
        decimal giftAmount, int? total, int? card)
    {
        var h = new Harness { GiftCardsEnabled = true };
        var cart = await CreateRewardCartAsync(h, 0);
        await SelectGiftTenderAsync(h, cart, giftAmount);
        var checkout = () => h.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card",
            ExpectedTotal: total is null ? null : (decimal)total,
            ExpectedCardAmount: card is null ? null : (decimal)card), Owner(cart));

        await checkout.Should().ThrowAsync<StorefrontValidationException>().WithMessage("*exact card remainder*");

        await using var ordering = h.Ordering();
        await using var commerce = h.Commerce();
        (await ordering.Orders.CountAsync()).Should().Be(0);
        (await commerce.OrderChargeSummaries.CountAsync()).Should().Be(0);
        (await commerce.InventoryReservations.CountAsync()).Should().Be(0);
        (await commerce.Carts.SingleAsync()).CheckoutPreparationJson.Should().BeNull();
        h.Payments.LastCommand.Should().BeNull();
    }

    [Theory]
    [InlineData(30, 140)]
    [InlineData(100, 0)]
    public async Task GiftTender_Should_FreezeActualItemIdsAndEarnOnlyOnCashFundedGoods(decimal giftAmount, long expectedPoints)
    {
        var h = new Harness { GiftCardsEnabled = true };
        var cart = await CreateRewardCartAsync(h, 0);
        await SelectGiftTenderAsync(h, cart, giftAmount);

        var result = await h.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card",
            ExpectedTotal: 100m, ExpectedCardAmount: 100m - giftAmount), Owner(cart));

        result.Total.Should().Be(100m);
        var command = h.Payments.LastCommand!;
        command.Amount.Should().Be(100m);
        command.GiftCard!.Tender!.ExpectedCardAmount.Should().Be(100m - giftAmount);
        var gift = command.GiftCard.Tender.Lines.Single();
        var loyalty = command.Loyalty!;
        var rewards = loyalty.Lines.Single();
        await using var ordering = h.Ordering();
        var item = await ordering.OrderItems.SingleAsync();
        gift.OrderItemId.Should().Be(item.Id);
        rewards.OrderItemId.Should().Be(item.Id);
        rewards.GiftFundedValue.Should().Be(giftAmount);
        rewards.NetPaidValue.Should().Be(100m - giftAmount);
        loyalty.EarnedPoints.Should().Be(expectedPoints);
        loyalty.PayableTotal.Should().Be(100m);
        await using var commerce = h.Commerce();
        var summary = await commerce.OrderChargeSummaries.SingleAsync();
        CheckoutGiftCards.Read(summary.GiftCardJson)!.Tender!.Lines.Single().Should().Be(gift);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GiftCheckout_Should_ResumeExactFrozenInstructionAfterProviderFailureAndPolicyChange(bool purchase)
    {
        var h = new Harness { GiftCardsEnabled = true };
        var cart = purchase ? await CreateGiftPurchaseCartAsync(h, "Email") : await CreateRewardCartAsync(h, 0);
        if (!purchase) await SelectGiftTenderAsync(h, cart, 30m);
        var access = purchase ? CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion) : Owner(cart);
        var commands = new List<CreateGuestPaymentIntentForOrderCommand>();
        h.Payments.BeforeCreate = command => { commands.Add(command); return Task.CompletedTask; };
        h.Payments.FailTimes = 1;
        var initial = () => h.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card",
            ExpectedTotal: purchase ? 50m : 100m, ExpectedCardAmount: purchase ? null : 70m), access);
        await initial.Should().ThrowAsync<InvalidOperationException>().WithMessage("*provider failure*");
        h.Gifts.Setup(g => g.GetPolicyAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Current policy must not replace a frozen attempt."));
        h.Gifts.Setup(g => g.QuoteAsync(It.IsAny<GiftCardQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Current balance must not replace a frozen attempt."));

        var result = await h.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card"), access);
        var replay = await h.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card"), access);

        commands.Should().HaveCount(2);
        commands[1].PaymentIntentId.Should().Be(commands[0].PaymentIntentId);
        commands[1].OrderId.Should().Be(commands[0].OrderId);
        JsonSerializer.Serialize(commands[1].GiftCard).Should().Be(JsonSerializer.Serialize(commands[0].GiftCard));
        replay.OrderId.Should().Be(result.OrderId);
        await using var ordering = h.Ordering();
        await using var commerce = h.Commerce();
        (await ordering.Orders.CountAsync()).Should().Be(1);
        (await commerce.OrderGiftCardDeliveries.CountAsync()).Should().Be(purchase ? 1 : 0);
    }

    private static readonly DeliveryAddressDto GiftPostalAddress = new("10 Gift Street", null, "London", null, "SW1A 1AA", "GB");

    private static GiftCardPolicy TestGiftPolicy() => new(true, "finance-gift-v1", "GBP",
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), new(true), "gift-terms-v1", GiftCardSettings.Proportional);

    private static async Task<CartDto> CreateGiftPurchaseCartAsync(Harness h, string method)
    {
        var product = await h.Products().CreateProductAsync(new("gift-value", "Gift card", ProductKinds.Variant,
            Variants: [new("GIFT", "Gift card")]));
        var variant = product.Variants.Single();
        var finance = TestGiftPolicy();
        h.Gifts.Setup(g => g.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(finance);
        var date = DateOnly.FromDateTime(h.Now).AddDays(1);
        var store = new GiftCardStorefrontOptions(true, "store-v1", variant.Id, [50m], DeliveryMethods: ["Email", "Post"],
            Postage: 3m, GreetingCardPrice: 2m, Timezone: "Europe/London", EmailSendTime: new(9, 0),
            PostingDays: [date.DayOfWeek], TaxTreatment: "ExcludeFaceValue");
        h.Settings.Setup(s => s.GetTenantValueAsync(GiftCardPurchasePricing.SettingName, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(store, GiftCardPurchasePricing.Json));
        var created = await h.Carts().CreateCartAsync(new("GBP"));
        await using var context = h.Commerce();
        var root = await context.Products.SingleAsync(p => p.Id == product.Id);
        root.Status = ProductStatuses.Active;
        root.IsPlaceholder = false;
        await context.SaveChangesAsync();
        var cart = await context.Carts.Include(c => c.Items).SingleAsync(c => c.Id == created.Id);
        var pricing = h.GiftPricing(context);
        var options = await pricing.OptionsAsync();
        var selection = new GiftCardPurchaseSelection(50m, method, "Recipient Person", options.Version,
            RecipientEmail: method == "Email" ? "recipient@example.test" : null,
            SendDate: method == "Email" ? date : null, PostingDate: method == "Post" ? date : null,
            PostalAddress: method == "Post" ? GiftPostalAddress : null,
            RecipientPhone: method == "Post" ? "+447700900123" : null,
            Message: "A gift for you", SenderName: "Purchaser Person", IncludeGreetingCard: method == "Post");
        var selected = await pricing.SelectAsync(cart, selection);
        cart.GiftCardPurchaseJson = JsonSerializer.Serialize(selected, GiftCardPurchasePricing.Json);
        cart.CheckoutDraftJson = CartDraftData.Serialize(new(Purchaser: new("purchaser@example.test", "Purchaser", "Person", "+447700900321")));
        context.CartItems.Add(new CartItem { TenantId = cart.TenantId, CartId = cart.Id, ProductVariantId = variant.Id,
            Quantity = 1m, UnitPriceSnapshot = 50m, LineKind = CartLineKinds.GiftCardValue, Sku = variant.Sku, NameSnapshot = variant.Name });
        await context.SaveChangesAsync();
        return created;
    }

    private static async Task SelectGiftTenderAsync(Harness h, CartDto cart, decimal giftAmount)
    {
        var policy = TestGiftPolicy();
        h.Gifts.Setup(g => g.GetPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        h.Gifts.Setup(g => g.QuoteAsync(It.IsAny<GiftCardQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GiftCardQuoteRequest request, CancellationToken _) =>
            {
                var line = request.Lines.Single();
                var tender = new GiftCardTender(request.PrivateCartGrant, request.RequestedAmount,
                    request.Total - request.RequestedAmount, request.TaxTotal, 0m,
                    [line with { GiftFundedValue = request.RequestedAmount }]);
                return new GiftCardFundingQuote(request.RequestedAmount, 100m, request.RequestedAmount,
                    request.Total - request.RequestedAmount, new(request.CartId, policy.Version, policy.Ledger!,
                        policy.Validity!, policy.TermsVersion!, policy.FundingAllocation!, Tender: tender));
            });
        await using var context = h.Commerce();
        var stored = await context.Carts.SingleAsync(c => c.Id == cart.Id);
        stored.GiftCardTenderJson = JsonSerializer.Serialize(new StoredGiftCardTender("private-fixture-grant", giftAmount, "•••• 1234"), GiftCardPurchasePricing.Json);
        await context.SaveChangesAsync();
    }
}
