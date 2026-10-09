using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class GuestOrderReadTests
{
    private static async Task<(BoxTestHarness Harness, BoxCartDto Box, CheckoutResult Checkout)> CheckoutAsync(
        Guid? partyId = null)
    {
        var harness = new BoxTestHarness();
        var fixture = await harness.BuildAsync("jollof");
        var carts = harness.BoxCarts();
        var box = await carts.CreateAsync(new CreateBoxCartCommand(fixture.BundleProductId, 6, BuyerPartyId: partyId));
        var access = partyId is { } party
            ? CartAccessContext.ForParty(party, "")
            : CartAccessContext.ForGuest(box.CartToken, box.CartVersion);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(fixture.DishVariants["jollof"], 6, null), access);
        access = await harness.HoldDeliveryAsync(box.Box.CartId, access);
        var checkout = await harness.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card",
            Delivery: BoxTestHarness.ValidDelivery), access);
        return (harness, box, checkout);
    }

    [Fact]
    public async Task Checkout_Should_IssueReusableGuestTokens_WithoutCreatingAnotherOrderOrPaymentOnReplay()
    {
        var (harness, box, first) = await CheckoutAsync();

        var replay = await harness.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), CartAccessContext.ForGuest(box.CartToken, box.CartVersion));

        first.GuestOrderToken.Should().NotBeNullOrWhiteSpace().And.NotBe(box.CartToken);
        replay.GuestOrderToken.Should().NotBeNullOrWhiteSpace();
        replay.OrderId.Should().Be(first.OrderId);
        replay.PaymentIntentId.Should().Be(first.PaymentIntentId);
        replay.Total.Should().Be(first.Total);
        harness.Payments.Calls.Should().Be(1);
        await using var ordering = harness.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(1);
        var orders = harness.StorefrontOrders();
        (await orders.GetGuestOrderAsync(first.OrderId, first.GuestOrderToken))!.OrderId.Should().Be(first.OrderId);
        (await orders.GetGuestOrderAsync(first.OrderId, replay.GuestOrderToken))!.OrderId.Should().Be(first.OrderId);
        (await orders.GetGuestOrderAsync(first.OrderId, first.GuestOrderToken)).Should().NotBeNull(
            "issuing a replay token must not revoke a confirmation page's earlier token");
    }

    [Fact]
    public async Task Checkout_Should_NotIssueGuestToken_ForPartyOwnedCartOrItsReplay()
    {
        var party = Guid.NewGuid();
        var (harness, box, first) = await CheckoutAsync(party);

        var replay = await harness.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), CartAccessContext.ForParty(party, ""));

        first.GuestOrderToken.Should().BeNull();
        replay.GuestOrderToken.Should().BeNull();
        (await harness.StorefrontOrders().GetMyOrderAsync(party, first.OrderId)).Should().NotBeNull();
        harness.Payments.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-cart-token")]
    [InlineData("order-token")]
    [InlineData("foreign-party")]
    public async Task Checkout_Should_RejectUnauthorizedReplay_WithoutDisclosingAGuestToken(string credential)
    {
        var (harness, box, first) = await CheckoutAsync();
        var access = credential switch
        {
            "wrong-cart-token" => CartAccessContext.ForGuest(CartAccess.MintToken()),
            "order-token" => CartAccessContext.ForGuest(first.GuestOrderToken),
            "foreign-party" => CartAccessContext.ForParty(Guid.NewGuid()),
            _ => CartAccessContext.ForGuest(null)
        };

        var replay = () => harness.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), access);

        await replay.Should().ThrowAsync<NotFoundException>();
        harness.Payments.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("malformed")]
    [InlineData("tampered")]
    [InlineData("cart-token")]
    [InlineData("wrong-order")]
    [InlineData("wrong-tenant")]
    public async Task GetGuestOrder_Should_ReturnNull_WhenTokenDoesNotAuthorizeThisTenantAndOrder(string credential)
    {
        var (harness, box, checkout) = await CheckoutAsync();
        var token = credential switch
        {
            "missing" => null,
            "empty" => " ",
            "malformed" => "not-a-protected-order-token",
            "tampered" => Tamper(checkout.GuestOrderToken!),
            "cart-token" => box.CartToken,
            "wrong-order" => harness.GuestOrderAccess.Issue(harness.TenantId, Guid.NewGuid()),
            "wrong-tenant" => harness.GuestOrderAccess.Issue(Guid.NewGuid(), checkout.OrderId),
            _ => throw new ArgumentOutOfRangeException(nameof(credential))
        };

        var result = await harness.StorefrontOrders().GetGuestOrderAsync(checkout.OrderId, token);

        result.Should().BeNull();
        harness.Payments.Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetGuestOrder_Should_NotReadAnotherTenant_EvenWithAnAuthenticTokenForThatTenant()
    {
        var (harness, _, checkout) = await CheckoutAsync();
        var otherTenantId = Guid.NewGuid();
        var otherTenant = new TestTenantProvider(otherTenantId);
        var service = new StorefrontOrderService(harness.Commerce(), otherTenant,
            new CoreOrderService(harness.Ordering(), otherTenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider(), new Aonik.TestSupport.Ordering.TestOrderNumberGenerator()),
            harness.GuestOrderAccess);

        var result = await service.GetGuestOrderAsync(checkout.OrderId, harness.GuestOrderAccess.Issue(otherTenantId, checkout.OrderId));

        result.Should().BeNull("the cart and charge summary belong to another tenant");
    }

    [Theory]
    [InlineData("cart")]
    [InlineData("summary")]
    [InlineData("order")]
    public async Task GetGuestOrder_Should_ReturnNull_WhenDurableCheckoutRecordIsMissing(string missing)
    {
        var (harness, box, checkout) = await CheckoutAsync();
        await using (var commerce = harness.Commerce())
        await using (var ordering = harness.Ordering())
        {
            if (missing == "cart")
                (await commerce.Carts.SingleAsync(c => c.Id == box.Box.CartId)).IsDeleted = true;
            if (missing == "summary")
                (await commerce.OrderChargeSummaries.SingleAsync(s => s.OrderId == checkout.OrderId)).IsDeleted = true;
            if (missing == "order")
                (await ordering.Orders.SingleAsync(o => o.Id == checkout.OrderId)).IsDeleted = true;
            await commerce.SaveChangesAsync();
            await ordering.SaveChangesAsync();
        }

        var result = await harness.StorefrontOrders().GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGuestOrder_Should_ObserveRecordedPaymentStatus_OnRepeatedPolling()
    {
        var (harness, _, checkout) = await CheckoutAsync();
        var orders = harness.StorefrontOrders();

        var pending = await orders.GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);
        pending!.PaymentStatus.Should().Be("Pending");
        await harness.Checkout().ConfirmPaymentAsync(checkout.OrderId, checkout.PaymentIntentId, checkout.Total, checkout.Currency);
        var paid = await orders.GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);
        var repeat = await orders.GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);

        paid!.PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        repeat.Should().BeEquivalentTo(paid);
        paid.Total.Should().Be(checkout.Total);
        harness.Payments.Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetGuestOrder_Should_PreserveIssuedToken_WhenGuestOrderIsLaterLinkedToAnAccount()
    {
        var (harness, box, checkout) = await CheckoutAsync();
        var party = Guid.NewGuid();
        await using (var commerce = harness.Commerce())
        {
            (await commerce.Carts.SingleAsync(c => c.Id == box.Box.CartId)).BuyerPartyId = party;
            await commerce.SaveChangesAsync();
        }

        var guest = await harness.StorefrontOrders().GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);
        var account = await harness.StorefrontOrders().GetMyOrderAsync(party, checkout.OrderId);

        guest.Should().NotBeNull().And.BeEquivalentTo(account);
    }

    [Fact]
    public async Task GetGuestOrder_Should_OnlyExposePublicSummary_AndLeaveCheckoutStateUnchanged()
    {
        var (harness, _, checkout) = await CheckoutAsync();
        var before = await CaptureStateAsync(harness);
        await using var commerce = harness.Commerce();
        await using var ordering = harness.Ordering();
        var tenant = new TestTenantProvider(harness.TenantId);
        var orders = new StorefrontOrderService(commerce, tenant,
            new CoreOrderService(ordering, tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider(), new Aonik.TestSupport.Ordering.TestOrderNumberGenerator()),
            harness.GuestOrderAccess);

        var result = await orders.GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);
        await orders.GetGuestOrderAsync(checkout.OrderId, checkout.GuestOrderToken);

        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(new[] {
            "orderId", "placedAtUtc", "status", "currency", "subtotal", "discountTotal", "taxTotal", "total",
            "boxSize", "items", "selections", "paymentStatus", "delivery", "orderNumber", "discountCode", "fulfilmentStatus", "loyalty" });
        result!.Loyalty.Should().BeNull();
        result!.Delivery!.Address.Should().BeEquivalentTo(BoxTestHarness.ValidDelivery.Address);
        result.Delivery.DeliveryDate.Should().Be(BoxTestHarness.ValidDelivery.DeliveryDate);
        json.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(new[] {
            "itemType", "quantity", "unitPrice", "amountIn", "sku", "name", "itemIndex" });
        json.GetProperty("selections")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(new[] {
            "productVariantId", "quantity", "sku", "personalisationSummary", "orderItemIndex", "name", "isSignature" });
        json.GetRawText().Should().NotContain("secret_box").And.NotContain("https://pay.example/box");
        commerce.ChangeTracker.Entries().Should().BeEmpty();
        ordering.ChangeTracker.Entries().Should().BeEmpty();
        (await CaptureStateAsync(harness)).Should().Be(before);
        harness.Payments.Calls.Should().Be(1);
    }

    [Fact]
    public void GuestOrderAccess_Should_ReusePlatformKeys_AndRejectTokensFromAnotherPurposeOrKeyRing()
    {
        var provider = new EphemeralDataProtectionProvider();
        var writer = new GuestOrderAccess(provider);
        var reader = new GuestOrderAccess(provider);
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var token = writer.Issue(tenantId, orderId);

        reader.IsValid(token, tenantId, orderId).Should().BeTrue();
        reader.IsValid(provider.CreateProtector("another-purpose").Protect(token), tenantId, orderId).Should().BeFalse();
        new GuestOrderAccess(new EphemeralDataProtectionProvider()).IsValid(token, tenantId, orderId).Should().BeFalse();
    }

    private static string Tamper(string token)
    {
        var middle = token.Length / 2;
        return token[..middle] + (token[middle] == 'A' ? 'B' : 'A') + token[(middle + 1)..];
    }

    private static async Task<string> CaptureStateAsync(BoxTestHarness harness)
    {
        await using var commerce = harness.Commerce();
        await using var ordering = harness.Ordering();
        return JsonSerializer.Serialize(new
        {
            Carts = await commerce.Carts.AsNoTracking().ToListAsync(),
            Summaries = await commerce.OrderChargeSummaries.AsNoTracking().ToListAsync(),
            Deliveries = await commerce.OrderDeliveryDetails.AsNoTracking().ToListAsync(),
            Reservations = await commerce.InventoryReservations.AsNoTracking().ToListAsync(),
            Orders = await ordering.Orders.AsNoTracking().ToListAsync(),
            Funding = await ordering.OrderFundingRefs.AsNoTracking().ToListAsync()
        });
    }
}
