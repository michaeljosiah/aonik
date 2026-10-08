using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public class CheckoutCoverageTests
{
    [Theory]
    [InlineData("missing-config", "commerce.delivery_unavailable")]
    [InlineData("disabled-config", "commerce.delivery_unavailable")]
    [InlineData("malformed-config", "commerce.delivery_unavailable")]
    [InlineData("not-served", "commerce.delivery_not_served")]
    [InlineData("provider-unavailable", "commerce.delivery_unavailable")]
    [InlineData("postcode-not-found", "commerce.invalid_postcode")]
    public async Task BoxCheckout_Should_RejectUnapprovedCoverage_BeforeChangingStockOrCreatingAnOrder(string condition, string code)
    {
        var (h, box, variantId) = await FullBoxAsync();
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);
        var delivery = BoxTestHarness.ValidDelivery;
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, Draft(delivery), access);
        await h.Inventory().ReserveAsync(box.Box.CartId, [new(variantId, 6)]);
        var coverage = new DeliveryCoverageServiceTests.Harness(h.TenantId);
        if (condition is not ("missing-config" or "malformed-config"))
            coverage.Configure(new(condition != "disabled-config", condition == "not-served" ? ["M1"] : ["SW1A"]));
        if (condition == "malformed-config") coverage.Values[h.TenantId] = "{";
        if (condition is "provider-unavailable" or "postcode-not-found")
            coverage.Lookup.Setup(service => service.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PostcodeLookupResult(condition == "provider-unavailable"
                    ? PostcodeLookupStatus.Unavailable : PostcodeLookupStatus.NotFound));
        await using var beforeContext = h.Commerce();
        var before = await beforeContext.Carts.AsNoTracking().SingleAsync();

        var checkout = () => h.Checkout(coverage.Service()).CheckoutAsync(new(box.Box.CartId, "Stripe", "Card"), access);

        (await checkout.Should().ThrowAsync<DeliveryCoverageException>()).Which.Code.Should().Be(code);
        h.Payments.Calls.Should().Be(0);
        await using var commerce = h.Commerce();
        var after = await commerce.Carts.SingleAsync();
        after.Status.Should().Be(CartStatuses.Open);
        after.OrderId.Should().BeNull();
        after.CheckoutDraftJson.Should().Be(before.CheckoutDraftJson);
        after.LastActivityAtUtc.Should().Be(before.LastActivityAtUtc);
        after.RowVersion.Should().Equal(before.RowVersion);
        var hold = await commerce.InventoryReservations.SingleAsync();
        hold.Status.Should().Be(InventoryReservationStatuses.Held);
        hold.Quantity.Should().Be(6m);
        var stock = await commerce.InventoryLevels.SingleAsync();
        stock.OnHand.Should().Be(10m);
        stock.Reserved.Should().Be(6m);
        (await commerce.OrderChargeSummaries.CountAsync()).Should().Be(0);
        (await commerce.OrderDeliveryDetails.CountAsync()).Should().Be(0);
        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoxCheckout_Should_CheckSelectedDelivery_AndPreserveTheDraftAndRecordedReplay(bool explicitDelivery)
    {
        var (h, box, _) = await FullBoxAsync();
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);
        var draftDelivery = BoxTestHarness.ValidDelivery;
        var saved = await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, Draft(draftDelivery), access);
        var coverage = new DeliveryCoverageServiceTests.Harness(h.TenantId);
        coverage.Configure(new(true, explicitDelivery ? ["M1"] : ["SW1A"]));
        var submitted = explicitDelivery ? draftDelivery with
        {
            Address = draftDelivery.Address with { Postcode = " m11aa ", CountryCode = "gb" }
        } : null;
        var expectedPostcode = explicitDelivery ? "M1 1AA" : "SW1A 1AA";

        var result = await h.Checkout(coverage.Service()).CheckoutAsync(
            new(box.Box.CartId, "Stripe", "Card", Delivery: submitted),
            CartAccessContext.ForGuest(box.CartToken, saved.CartVersion));

        coverage.Lookup.Verify(service => service.LookupAsync(expectedPostcode, It.IsAny<CancellationToken>()), Times.Once);
        await using var context = h.Commerce();
        (await context.OrderDeliveryDetails.SingleAsync()).Postcode.Should().Be(expectedPostcode);
        CartDraftData.Read(await context.Carts.SingleAsync()).Should().Be(saved.Draft);
        coverage.Values[h.TenantId] = null;
        var replay = await h.Checkout(coverage.Service()).CheckoutAsync(new(box.Box.CartId, "", "",
            Delivery: draftDelivery with { Address = draftDelivery.Address with { CountryCode = "US" } }),
            CartAccessContext.ForGuest(box.CartToken));
        replay.OrderId.Should().Be(result.OrderId);
        replay.PaymentIntentId.Should().Be(result.PaymentIntentId);
        h.Payments.Calls.Should().Be(1);
        coverage.Lookup.Verify(service => service.LookupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("IE")]
    [InlineData("IM")]
    [InlineData("GG")]
    [InlineData("JE")]
    public async Task BoxCheckout_Should_NotUseAUkPostcodeToApproveAnotherCountry(string country)
    {
        var (h, box, _) = await FullBoxAsync();
        var coverage = new Mock<IDeliveryCoverageService>(MockBehavior.Strict);
        var delivery = BoxTestHarness.ValidDelivery;
        var checkout = () => h.Checkout(coverage.Object).CheckoutAsync(new(box.Box.CartId, "Stripe", "Card",
            Delivery: delivery with { Address = delivery.Address with { CountryCode = country } }),
            CartAccessContext.ForGuest(box.CartToken, box.CartVersion));

        (await checkout.Should().ThrowAsync<DeliveryCoverageException>()).Which.Code.Should().Be("commerce.unsupported_delivery_country");
        coverage.VerifyNoOtherCalls();
        h.Payments.Calls.Should().Be(0);
        await using var context = h.Commerce();
        (await context.InventoryReservations.CountAsync()).Should().Be(0);
        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericCheckout_Should_NotRequireCoverage_EvenWithAnOptionalNonGbDelivery(bool shipping)
    {
        var h = new BoxTestHarness();
        var catalog = await h.BuildAsync("dish");
        var variant = catalog.DishVariants["dish"];
        await h.Pricing().SetPriceAsync(new(variant, "GBP", 10m));
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var access = CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion);
        await h.Carts().AddItemAsync(new(cart.Id, variant), access);
        var coverage = new Mock<IDeliveryCoverageService>(MockBehavior.Strict);
        var delivery = BoxTestHarness.ValidDelivery;

        var result = await h.Checkout(coverage.Object).CheckoutAsync(new(cart.Id, "Stripe", "Card",
            Delivery: shipping ? delivery with { Address = delivery.Address with { CountryCode = "US" } } : null), access);

        result.Total.Should().Be(10m);
        coverage.VerifyNoOtherCalls();
        h.Payments.Calls.Should().Be(1);
    }

    [Fact]
    public async Task BoxCheckout_Should_AuthorizeAndRequireVersion_BeforeCheckingCoverage()
    {
        var (h, box, _) = await FullBoxAsync();
        var coverage = new Mock<IDeliveryCoverageService>(MockBehavior.Strict);
        var command = new CheckoutCommand(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery);
        var unauthorized = () => h.Checkout(coverage.Object).CheckoutAsync(command, CartAccessContext.ForGuest("wrong"));
        var unversioned = () => h.Checkout(coverage.Object).CheckoutAsync(command, CartAccessContext.ForGuest(box.CartToken));

        await unauthorized.Should().ThrowAsync<NotFoundException>();
        await unversioned.Should().ThrowAsync<CartWriteConflictException>();
        coverage.VerifyNoOtherCalls();
        h.Payments.Calls.Should().Be(0);
    }

    private static CartCheckoutDraftDto Draft(CheckoutDeliveryDetails delivery)
        => new(delivery.Purchaser, delivery.Address, delivery.Recipient, delivery.DeliveryDate, delivery.Notes);

    private static async Task<(BoxTestHarness Harness, BoxCartDto Box, Guid VariantId)> FullBoxAsync()
    {
        var h = new BoxTestHarness();
        var catalog = await h.BuildAsync("dish");
        var variant = catalog.DishVariants["dish"];
        var box = await h.BoxCarts().CreateAsync(new(catalog.BundleProductId, 6, new(variant, 6)));
        return (h, box, variant);
    }
}
