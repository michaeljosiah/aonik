using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class GiftDraftTests
{
    [Fact]
    public async Task Save_Should_PreserveFoodExtrasAndDelivery_WhenGiftIsChangedOrRemoved()
    {
        var h = new BoxTestHarness();
        h.Settings[CommerceSettingNames.StorefrontGreetingCard] = """{"isEnabled":true,"currency":"GBP","amount":3}""";
        var fixture = await h.BuildAsync("dish");
        var extra = await h.AddExtraAsync("sauce", 5m);
        var created = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6,
            new(fixture.DishVariants["dish"], 6)));
        var access = CartAccessContext.ForGuest(created.CartToken, created.CartVersion);
        var withExtra = await h.BoxCarts().AddExtraLineAsync(created.Box.CartId, new(extra.VariantId, 2), access);
        access = access with { ExpectedCartVersion = withExtra.CartVersion };
        var delivery = BoxTestHarness.ValidDelivery;
        var draft = new CartCheckoutDraftDto(delivery.Purchaser, delivery.Address,
            new DeliveryRecipientDto("Gift Recipient", "+44 7700 900124"), delivery.DeliveryDate,
            Notes: "Ring the bell", Gift: new(GiftIntent: true, IncludeGreetingCard: true,
                GreetingCardMessage: "  Happy birthday!\nEnjoy your meal.  "), CreateAccount: true);

        var saved = await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, draft, access);
        access = access with { ExpectedCartVersion = saved.CartVersion };
        var resumed = await h.BoxCarts().GetAsync(created.Box.CartId, access);

        JsonSerializer.Serialize(resumed.Box).Should().Be(JsonSerializer.Serialize(withExtra.Box));
        resumed.CheckoutDraft.Should().Be(saved.Draft);
        saved.Draft!.Gift!.HidePrices.Should().BeTrue();
        saved.Draft.Gift.GreetingCardMessage.Should().Be("Happy birthday!\nEnjoy your meal.");

        var withoutCard = await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, saved.Draft with
        {
            Gift = saved.Draft.Gift with { HidePrices = false, IncludeGreetingCard = false }
        }, access);
        access = access with { ExpectedCartVersion = withoutCard.CartVersion };
        var cardRemoved = await h.BoxCarts().GetAsync(created.Box.CartId, access);
        cardRemoved.CheckoutDraft!.Gift!.HidePrices.Should().BeFalse();
        cardRemoved.CheckoutDraft.Gift.GreetingCardMessage.Should().BeNull();
        JsonSerializer.Serialize(cardRemoved.Box).Should().Be(JsonSerializer.Serialize(withExtra.Box));

        var withoutGift = await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, withoutCard.Draft! with
        {
            Gift = new(GiftIntent: false, IncludeGreetingCard: true, GreetingCardMessage: "ignored")
        }, access);
        var giftRemoved = await h.BoxCarts().GetAsync(created.Box.CartId,
            access with { ExpectedCartVersion = withoutGift.CartVersion });
        giftRemoved.CheckoutDraft.Should().Be(draft with { Gift = null });
        JsonSerializer.Serialize(giftRemoved.Box).Should().Be(JsonSerializer.Serialize(withExtra.Box));
        h.Payments.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public async Task Save_Should_NotRenewOrReplaceSameDateHold_WhenGiftDetailsChange(int elapsedMinutes)
    {
        var h = new BoxTestHarness();
        h.Settings[CommerceSettingNames.StorefrontGreetingCard] = """{"isEnabled":true,"currency":"GBP","amount":3}""";
        var fixture = await h.BuildAsync("dish");
        var created = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6,
            new(fixture.DishVariants["dish"], 6)));
        var access = CartAccessContext.ForGuest(created.CartToken, created.CartVersion);
        var saved = await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId,
            new(DeliveryDate: BoxTestHarness.ValidDelivery.DeliveryDate, Gift: new(GiftIntent: true)), access);
        access = access with { ExpectedCartVersion = saved.CartVersion };
        await using var before = h.Commerce();
        var originalHold = await before.CartDeliveryReservations.AsNoTracking().SingleAsync();
        originalHold.ExpiresAtUtc.Should().Be(originalHold.SelectedAtUtc.AddMinutes(15));
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(elapsedMinutes);

        var updated = await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, saved.Draft! with
        {
            Gift = new(GiftIntent: true, HidePrices: false, IncludeGreetingCard: true,
                GreetingCardMessage: "Thank you!")
        }, access);
        await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, updated.Draft! with { Gift = null },
            access with { ExpectedCartVersion = updated.CartVersion });

        await using var verify = h.Commerce();
        var hold = await verify.CartDeliveryReservations.AsNoTracking().SingleAsync();
        hold.Should().BeEquivalentTo(originalHold, options => options.ComparingByMembers<CartDeliveryReservation>(),
            "same-date gift edits cannot extend a capacity hold, even after it expires");
        (await verify.Carts.SingleAsync()).LastActivityAtUtc.Should().Be(h.Clock.UtcNow);
        (await h.Carts().GetCartAsync(created.Box.CartId, access))!.CheckoutDraft!.DeliveryDate
            .Should().Be(originalHold.DeliveryDate);
    }
}
