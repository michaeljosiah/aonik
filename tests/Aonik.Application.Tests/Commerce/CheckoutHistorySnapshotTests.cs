using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class CheckoutHistorySnapshotTests
{
    private const string Terms = """{"version":"sale-v1","url":"https://food.example/terms/v1"}""";

    [Theory]
    [InlineData(null)]
    [InlineData("old-version")]
    public async Task ConfiguredTerms_Should_RequireExplicitCurrentAcceptance_BeforeAnyOrderOrPayment(string? version)
    {
        var (h, box, access) = await ArrangeAsync(version);
        var checkout = () => h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), access);

        await checkout.Should().ThrowAsync<StorefrontValidationException>().WithMessage("AcceptedTermsVersion:*");

        h.Payments.Calls.Should().Be(0);
        await using var db = h.Commerce();
        (await db.OrderChargeSummaries.AnyAsync()).Should().BeFalse();
        (await db.InventoryReservations.AnyAsync()).Should().BeFalse();
        await using var orders = h.Ordering();
        (await orders.Orders.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Checkout_Should_FreezePurchasedNamesSignatureAndTerms_ThroughCatalogueChangesAndReplay()
    {
        var (h, box, access) = await ArrangeAsync("sale-v1");
        var result = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), access);
        await using (var db = h.Commerce())
        {
            var purchased = await db.OrderBundleSelections.SingleAsync();
            purchased.NameSnapshot.Should().Be("jollof");
            purchased.IsSignatureSnapshot.Should().BeTrue();
            var snapshot = await db.OrderDeliveryDetails.SingleAsync();
            snapshot.AcceptedTermsVersion.Should().Be("sale-v1");
            snapshot.AcceptedTermsUrl.Should().Be("https://food.example/terms/v1");
            snapshot.TermsAcceptedAtUtc.Should().Be(h.Clock.UtcNow);
            var product = await db.Products.SingleAsync(x => x.Slug == "jollof");
            product.Name = "Renamed later";
            product.TagsJson = "[]";
            await db.SaveChangesAsync();
        }
        h.Settings[CommerceSettingNames.StorefrontSaleTerms] = "invalid new configuration";
        h.Settings.Remove(CommerceSettingNames.StorefrontSignatureTag);

        var replay = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card"), access);
        var read = await h.StorefrontOrders().GetGuestOrderAsync(result.OrderId, result.GuestOrderToken);

        replay.PaymentIntentId.Should().Be(result.PaymentIntentId);
        h.Payments.Calls.Should().Be(1);
        read!.Selections.Single().Name.Should().Be("jollof");
        read.Selections.Single().IsSignature.Should().BeTrue();
        read.Items.Single().Name.Should().Be("6-dish box");
        read.OrderNumber.Should().NotBeNullOrWhiteSpace();
        read.Delivery!.SaleTerms.Should().Be(new AcceptedSaleTermsDto("sale-v1", "https://food.example/terms/v1", h.Clock.UtcNow));
    }

    [Fact]
    public async Task ProvenUnpaidRecovery_Should_RequireNewTerms_AndReplaceTheSnapshotOnTheSameOrder()
    {
        var (h, box, access) = await ArrangeAsync("sale-v1");
        var first = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), access);
        var recovery = await h.Checkout().RecoverAsync(box.Box.CartId, first.PaymentIntentId, access);
        recovery.CanEdit.Should().BeTrue();
        h.Settings[CommerceSettingNames.StorefrontSaleTerms] = """{"version":"sale-v2","url":"https://food.example/terms/v2"}""";
        var nextAccess = await h.HoldDeliveryAsync(box.Box.CartId, access with { ExpectedCartVersion = recovery.CartVersion });
        var staleAcceptance = () => h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), nextAccess);

        await staleAcceptance.Should().ThrowAsync<StorefrontValidationException>().WithMessage("AcceptedTermsVersion:*");
        h.Payments.Calls.Should().Be(1);
        var saved = await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId,
            new(DeliveryDate: BoxTestHarness.ValidDelivery.DeliveryDate, AcceptedTermsVersion: "sale-v2"), nextAccess);
        var replacement = await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery),
            nextAccess with { ExpectedCartVersion = saved.CartVersion });

        replacement.OrderId.Should().Be(first.OrderId);
        replacement.PaymentIntentId.Should().NotBe(first.PaymentIntentId);
        await using var verify = h.Commerce();
        var snapshot = await verify.OrderDeliveryDetails.SingleAsync();
        snapshot.AcceptedTermsVersion.Should().Be("sale-v2");
        snapshot.AcceptedTermsUrl.Should().Be("https://food.example/terms/v2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task SignatureWithoutAuthoredDesignation_Should_RemainUnknown_EvenWithSurcharge(string? tag)
    {
        var (h, box, access) = await ArrangeAsync("sale-v1");
        if (tag is null) h.Settings.Remove(CommerceSettingNames.StorefrontSignatureTag);
        else h.Settings[CommerceSettingNames.StorefrontSignatureTag] = tag;
        await using (var db = h.Commerce())
        {
            var product = await db.Products.SingleAsync(x => x.Slug == "jollof");
            product.UnitSurcharge = 2;
            product.UnitSurchargeCurrency = "GBP";
            await db.SaveChangesAsync();
        }
        // Refresh the existing box drift path before acknowledging its new price.
        var refreshed = await h.BoxCarts().GetAsync(box.Box.CartId, access);
        await h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery,
            ExpectedTotal: refreshed.Quote.Total), access with { ExpectedCartVersion = refreshed.CartVersion });

        await using var verify = h.Commerce();
        (await verify.OrderBundleSelections.SingleAsync()).IsSignatureSnapshot.Should().BeNull();
    }

    [Theory]
    [InlineData("{}")] 
    [InlineData("""{"version":"v1","url":"javascript:alert(1)"}""")]
    [InlineData("""{"version":"v1","url":"https://user:password@food.example/terms"}""")]
    public async Task MalformedTerms_Should_NotPublishOrPermitCheckout(string configured)
    {
        var h = new BoxTestHarness();
        h.Settings[CommerceSettingNames.StorefrontSaleTerms] = configured;
        var settings = new DictionaryTenantSettingStore(h.Settings);

        (await SaleTermsPolicy.ReadAsync(settings, h.TenantId)).Should().BeNull();
        var accept = () => SaleTermsPolicy.AcceptAsync(settings, h.TenantId, "v1", h.Clock.UtcNow);
        await accept.Should().ThrowAsync<StorefrontValidationException>();
    }

    private static async Task<(BoxTestHarness H, BoxCartDto Box, CartAccessContext Access)> ArrangeAsync(string? version)
    {
        var h = new BoxTestHarness();
        h.Settings[CommerceSettingNames.StorefrontSaleTerms] = Terms;
        h.Settings[CommerceSettingNames.StorefrontSignatureTag] = "Signature";
        var fixture = await h.BuildAsync("jollof");
        await using (var db = h.Commerce())
        {
            var dish = await db.Products.SingleAsync(x => x.Id == fixture.DishProducts["jollof"]);
            dish.TagsJson = "[\"Signature\"]";
            await db.SaveChangesAsync();
        }
        var box = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6, new(fixture.DishVariants["jollof"], 6)));
        box = await h.HoldDeliveryAsync(box);
        var access = CartAccessContext.ForGuest(box.CartToken, box.CartVersion);
        var saved = await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId,
            new(DeliveryDate: BoxTestHarness.ValidDelivery.DeliveryDate, AcceptedTermsVersion: version), access);
        return (h, box, access with { ExpectedCartVersion = saved.CartVersion });
    }
}
