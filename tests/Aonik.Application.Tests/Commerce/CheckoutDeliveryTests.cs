using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class CheckoutDeliveryTests
{
    [Fact]
    public async Task Checkout_Should_FreezePurchaserRecipientAddressAndCalendar_AndReplayWithoutRevalidation()
    {
        var (harness, box) = await FullBoxAsync();
        var input = BoxTestHarness.ValidDelivery with
        {
            Purchaser = new(" Buyer@Example.test ", " Ada ", " Customer ", " +44 7700 900111 "),
            Address = new(" 12 Sample Street ", " ", " London ", " Greater London ", " sw1a 1aa ", " gb "),
            Recipient = new(" Sam Recipient ", " 07700 900222 "),
            Notes = " Please ring the bell.\nLeave with reception. "
        };
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);

        var result = await harness.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: input), access);

        await using var context = harness.Commerce();
        var snapshot = await context.OrderDeliveryDetails.SingleAsync();
        snapshot.OrderId.Should().Be(result.OrderId);
        snapshot.TenantId.Should().Be(harness.TenantId);
        snapshot.PurchaserEmail.Should().Be("Buyer@Example.test");
        snapshot.PurchaserFirstName.Should().Be("Ada");
        snapshot.PurchaserPhone.Should().Be("+44 7700 900111");
        snapshot.RecipientName.Should().Be("Sam Recipient");
        snapshot.RecipientPhone.Should().Be("07700 900222");
        snapshot.AddressLine1.Should().Be("12 Sample Street");
        snapshot.AddressLine2.Should().BeNull();
        snapshot.Postcode.Should().Be("SW1A 1AA");
        snapshot.CountryCode.Should().Be("GB");
        snapshot.DeliveryDate.Should().Be(input.DeliveryDate);
        snapshot.Timezone.Should().Be("Europe/London");
        snapshot.Notes.Should().Be("Please ring the bell.\nLeave with reception.");
        var calendar = await context.FulfilmentCalendars.SingleAsync();
        calendar.IsActive = false;
        calendar.Timezone = "UTC";
        await context.SaveChangesAsync();

        var replay = await harness.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card",
            Delivery: input with { DeliveryDate = default, Purchaser = null! }), access);

        replay.Should().BeEquivalentTo(result, options => options.Excluding(x => x.GuestOrderToken));
        harness.GuestOrderAccess.IsValid(replay.GuestOrderToken, harness.TenantId, result.OrderId).Should().BeTrue();
        harness.Payments.Calls.Should().Be(1);
        await context.Entry(snapshot).ReloadAsync();
        snapshot.RecipientName.Should().Be("Sam Recipient");
        snapshot.Timezone.Should().Be("Europe/London");
        (await context.OrderDeliveryDetails.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Checkout_Should_UseSubmittedPurchaserAsRecipient_WhenRecipientIsOmitted()
    {
        var (harness, box) = await FullBoxAsync();

        await harness.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));

        await using var context = harness.Commerce();
        var row = await context.OrderDeliveryDetails.SingleAsync();
        row.RecipientName.Should().Be("Pat Customer");
        row.RecipientPhone.Should().Be(BoxTestHarness.ValidDelivery.Purchaser.Phone);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("purchaser")]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("address")]
    [InlineData("country")]
    [InlineData("recipient")]
    [InlineData("window")]
    [InlineData("notes")]
    [InlineData("control")]
    [InlineData("missing-date")]
    [InlineData("too-early")]
    [InlineData("wrong-weekday")]
    [InlineData("blackout")]
    [InlineData("inactive-calendar")]
    public async Task Checkout_Should_RejectInvalidDelivery_BeforeInventoryOrderOrPaymentEffects(string invalid)
    {
        var (harness, box) = await FullBoxAsync();
        var valid = BoxTestHarness.ValidDelivery;
        CheckoutDeliveryDetails? input = invalid switch
        {
            "missing" => null,
            "purchaser" => valid with { Purchaser = null! },
            "email" => valid with { Purchaser = valid.Purchaser with { Email = "not-an-email" } },
            "phone" => valid with { Purchaser = valid.Purchaser with { Phone = "words-only" } },
            "address" => valid with { Address = null! },
            "country" => valid with { Address = valid.Address with { CountryCode = "UKK" } },
            "recipient" => valid with { Recipient = new("", "+44 7700 900123") },
            "window" => valid with { WindowId = "morning" },
            "notes" => valid with { Notes = new string('x', 1001) },
            "control" => valid with { Address = valid.Address with { Line1 = "street\u0000" } },
            "missing-date" => valid with { DeliveryDate = default },
            "too-early" => valid with { DeliveryDate = valid.DeliveryDate.AddDays(-7) },
            "wrong-weekday" => valid with { DeliveryDate = valid.DeliveryDate.AddDays(1) },
            _ => valid
        };
        if (invalid is "blackout" or "inactive-calendar")
        {
            await using var setup = harness.Commerce();
            var calendar = await setup.FulfilmentCalendars.SingleAsync();
            if (invalid == "blackout") calendar.BlackoutDatesJson = "[\"2026-06-25\"]";
            else calendar.IsActive = false;
            await setup.SaveChangesAsync();
        }

        var attempt = () => harness.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: input),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));

        await attempt.Should().ThrowAsync<StorefrontValidationException>();
        harness.Payments.Calls.Should().Be(0);
        await using var commerce = harness.Commerce();
        await using var ordering = harness.Ordering();
        (await commerce.OrderDeliveryDetails.CountAsync()).Should().Be(0);
        (await commerce.OrderChargeSummaries.CountAsync()).Should().Be(0);
        (await commerce.InventoryReservations.CountAsync()).Should().Be(0);
        (await commerce.InventoryLevels.SumAsync(x => x.Reserved)).Should().Be(0);
        (await ordering.Orders.CountAsync()).Should().Be(0);
        (await commerce.Carts.SingleAsync()).OrderId.Should().BeNull();
    }

    [Fact]
    public async Task Checkout_Should_AuthorizeCart_BeforeValidatingItsDelivery()
    {
        var (harness, box) = await FullBoxAsync();
        var attempt = () => harness.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card"), CartAccessContext.ForGuest("wrong-token"));

        await attempt.Should().ThrowAsync<NotFoundException>();
        harness.Payments.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Checkout_Should_AlsoSnapshotDelivery_WhenAGenericCartSuppliesIt()
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("dish");
        var variant = fixture.DishVariants["dish"];
        await harness.Pricing().SetPriceAsync(new SetPriceCommand(variant, "GBP", 10m));
        var cart = await harness.Carts().CreateCartAsync(new("GBP"));
        var access = CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion);
        await harness.Carts().AddItemAsync(new(cart.Id, variant), access);

        var result = await harness.Checkout().CheckoutAsync(new(cart.Id, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), access);

        await using var context = harness.Commerce();
        (await context.OrderDeliveryDetails.SingleAsync()).OrderId.Should().Be(result.OrderId);
        result.Total.Should().Be(10m);
    }

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box)> FullBoxAsync()
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("dish");
        var box = await harness.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6));
        await harness.BoxCarts().AddLineAsync(box.Box.CartId, new(fixture.DishVariants["dish"], 6, null),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));
        return (harness, box);
    }
}
