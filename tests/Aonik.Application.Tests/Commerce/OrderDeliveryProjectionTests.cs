using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Commerce;

public class OrderDeliveryProjectionTests
{
    [Fact]
    public async Task History_Should_UseOnlyPurchasedNamesTermsAndExplicitFulfilment_WithoutLiveCatalogueRows()
    {
        var h = new BoxTestHarness();
        var party = Guid.NewGuid();
        var id = await SeedOrderAsync(h, party, new DateOnly(2020, 1, 1));
        var acceptedAt = new DateTime(2019, 12, 1, 10, 0, 0, DateTimeKind.Utc);
        await using (var ordering = h.Ordering())
        {
            var order = await ordering.Orders.Include(o => o.Items).SingleAsync(o => o.Id == id);
            order.OrderNumber = "BOX-2019-000042";
            order.Items.Single().NameSnapshot = "The purchased winter box";
            await ordering.SaveChangesAsync();
        }
        await using (var db = h.Commerce())
        {
            var delivery = await db.OrderDeliveryDetails.SingleAsync(d => d.OrderId == id);
            delivery.AcceptedTermsVersion = "winter-2019";
            delivery.AcceptedTermsUrl = "https://example.test/terms/winter-2019";
            delivery.TermsAcceptedAtUtc = acceptedAt;
            var charge = await db.OrderChargeSummaries.SingleAsync(s => s.OrderId == id);
            charge.DiscountCode = "WINTER";
            charge.DiscountTotal = 5;
            charge.Total -= 5;
            db.OrderBundleSelections.Add(new OrderBundleSelection
            {
                TenantId = h.TenantId, OrderId = id, ProductVariantId = Guid.NewGuid(), BundleSlotId = Guid.NewGuid(),
                Sku = "RETIRED-DISH", NameSnapshot = "Original recipe", IsSignatureSnapshot = true,
                Quantity = 6, PersonalisationSummary = "Mild",
            });
            await db.SaveChangesAsync();
        }

        var detail = (await h.StorefrontOrders().GetMyOrderAsync(party, id))!;
        detail.OrderNumber.Should().Be("BOX-2019-000042");
        detail.Items.Single().Name.Should().Be("The purchased winter box");
        detail.Selections.Single().Name.Should().Be("Original recipe");
        detail.Selections.Single().IsSignature.Should().BeTrue();
        detail.DiscountCode.Should().Be("WINTER");
        detail.DiscountTotal.Should().Be(5);
        detail.Delivery!.SaleTerms.Should().Be(new AcceptedSaleTermsDto("winter-2019", "https://example.test/terms/winter-2019", acceptedAt));
        var row = (await h.StorefrontOrders().ListMyOrdersAsync(party)).Items.Single();
        row.HistoryGroup.Should().Be("Upcoming", "a past scheduled date is not evidence of delivery");
        row.FulfilmentStatus.Should().Be("Confirmed");
        row.Selections.Should().BeEquivalentTo(detail.Selections);
        var admin = (await AdminService(h).GetOrderStorefrontAsync(id))!;
        admin.Fulfilment!.History.Should().BeEmpty("payment confirms the order without inventing a staff timestamp");
        admin.Items.Single().Name.Should().Be(detail.Items.Single().Name);
        (await AdminService(h).GetOrderPackingAsync(id))!.OrderNumber.Should().Be(detail.OrderNumber);

        await using var update = h.Commerce();
        (await update.OrderDeliveryDetails.SingleAsync(d => d.OrderId == id)).FulfilmentStatus = "Delivered";
        await update.SaveChangesAsync();
        (await h.StorefrontOrders().ListMyOrdersAsync(party)).Items.Single().HistoryGroup.Should().Be("Past");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GiftReads_Should_KeepHistoricalMoneyAndGiftFacts_WhenDraftAndCurrentFeeChange(bool hidePrices)
    {
        var h = new BoxTestHarness();
        var partyId = Guid.NewGuid();
        var gift = new OrderGiftDto(hidePrices, true, "Happy birthday!\nWith love <3");
        var orderId = await SeedOrderAsync(h, partyId, new DateOnly(2026, 10, 25), gift);
        h.Settings["Commerce.Storefront.GreetingCard"] = """{"isEnabled":true,"currency":"GBP","amount":8}""";
        await using (var db = h.Commerce())
        {
            var cart = await db.Carts.SingleAsync(c => c.OrderId == orderId);
            cart.CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto());
            await db.SaveChangesAsync();
        }

        var customer = await h.StorefrontOrders().GetMyOrderAsync(partyId, orderId);
        var guest = await h.StorefrontOrders().GetGuestOrderAsync(orderId, h.GuestOrderAccess.Issue(h.TenantId, orderId));
        var admin = AdminService(h);
        var financial = await admin.GetOrderStorefrontAsync(orderId);
        var packing = await admin.GetOrderPackingAsync(orderId);

        customer!.Delivery!.Gift.Should().Be(gift);
        guest!.Delivery!.Gift.Should().Be(gift);
        financial!.Delivery!.Gift.Should().Be(gift);
        packing!.Gift.Should().Be(gift);
        customer.Total.Should().Be(98m);
        guest.Total.Should().Be(98m);
        financial.Charge.Total.Should().Be(98m);
        financial.Items.Single(item => item.ItemType == CheckoutService.GreetingCardItemType).Amount.Should().Be(3m);
        (packing.Prices is null).Should().Be(hidePrices);
        if (!hidePrices) packing.Prices!.Items.Single(item => item.ItemIndex == 1).Amount.Should().Be(3m);
        (await h.StorefrontOrders().ListMyOrdersAsync(partyId)).Items.Single().IsGift.Should().BeTrue();
        (await admin.ListOrdersAsync()).Items.Single().IsGift.Should().BeTrue();
        (await AdminService(h, Guid.NewGuid()).GetOrderPackingAsync(orderId)).Should().BeNull();
    }

    [Fact]
    public async Task OrderReads_Should_ReturnTheSameRecordedDelivery_ToOwnerGuestAndAdmin()
    {
        var h = new BoxTestHarness();
        var partyId = Guid.NewGuid();
        var orderId = await SeedOrderAsync(h, partyId, new DateOnly(2026, 10, 25));
        var token = h.GuestOrderAccess.Issue(h.TenantId, orderId);

        var customer = await h.StorefrontOrders().GetMyOrderAsync(partyId, orderId);
        var guest = await h.StorefrontOrders().GetGuestOrderAsync(orderId, token);
        var admin = await AdminService(h).GetOrderStorefrontAsync(orderId);

        var expected = new OrderDeliveryDto(
            new CheckoutContactDto("purchaser@example.com", "Ada", "Cook", "+44 20 1111 1111"),
            new DeliveryAddressDto("10 Kitchen Road", "Flat 2", "London", null, "SW1A 1AA", "GB"),
            new DateOnly(2026, 10, 25), "Europe/London",
            new DeliveryRecipientDto("Sam Recipient", "+44 20 2222 2222"), "Ring the bell\nLeave with reception");
        customer!.Delivery.Should().BeEquivalentTo(expected);
        guest!.Delivery.Should().BeEquivalentTo(expected);
        admin!.Delivery.Should().BeEquivalentTo(expected);
        // No Party or current address record exists in this fixture: reads must use the snapshot.
        await using var verify = h.Commerce();
        (await verify.OrderDeliveryDetails.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Lists_Should_PageDeliveryDates_WithoutExposingContactDetails()
    {
        var h = new BoxTestHarness();
        var partyId = Guid.NewGuid();
        var first = await SeedOrderAsync(h, partyId, new DateOnly(2026, 11, 1));
        var second = await SeedOrderAsync(h, partyId, new DateOnly(2026, 11, 8));
        var legacy = await SeedOrderAsync(h, partyId, null);
        var otherParty = await SeedOrderAsync(h, Guid.NewGuid(), new DateOnly(2026, 11, 15));
        var expected = new Dictionary<Guid, DateOnly?>
        {
            [first] = new(2026, 11, 1), [second] = new(2026, 11, 8), [legacy] = null,
            [otherParty] = new(2026, 11, 15),
        };

        var customerPages = new List<StorefrontOrderSummaryDto>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await h.StorefrontOrders().ListMyOrdersAsync(partyId, page, pageSize: 1);
            result.TotalCount.Should().Be(3);
            customerPages.Add(result.Items.Should().ContainSingle().Subject);
        }
        customerPages.Select(row => row.OrderId).Should().BeEquivalentTo(new[] { first, second, legacy });
        customerPages.Should().OnlyContain(row => row.DeliveryDate == expected[row.OrderId]);
        (await h.StorefrontOrders().ListMyOrdersAsync(partyId, page: 4, pageSize: 1)).Items.Should().BeEmpty();

        var admin = AdminService(h);
        var adminPages = new List<AdminStorefrontOrderRowDto>();
        for (var page = 1; page <= 2; page++)
        {
            var result = await admin.ListOrdersAsync(page: page, pageSize: 2);
            result.TotalCount.Should().Be(4);
            adminPages.AddRange(result.Items);
        }
        adminPages.Select(row => row.OrderId).Should().BeEquivalentTo(expected.Keys);
        adminPages.Should().OnlyContain(row => row.DeliveryDate == expected[row.OrderId]);
        (await admin.GetPartyStorefrontAsync(partyId)).Orders.Should().BeEquivalentTo(customerPages);

        var json = JsonSerializer.Serialize(new { customerPages, adminPages });
        json.Should().NotContain("purchaser@example.com").And.NotContain("Kitchen Road")
            .And.NotContain("Sam Recipient").And.NotContain("Ring the bell");
    }

    [Fact]
    public async Task LegacyOrder_Should_RemainReadable_WithNoInventedDelivery()
    {
        var h = new BoxTestHarness();
        var partyId = Guid.NewGuid();
        var orderId = await SeedOrderAsync(h, partyId, null);

        var customer = await h.StorefrontOrders().GetMyOrderAsync(partyId, orderId);
        var guest = await h.StorefrontOrders().GetGuestOrderAsync(orderId, h.GuestOrderAccess.Issue(h.TenantId, orderId));
        var admin = await AdminService(h).GetOrderStorefrontAsync(orderId);

        customer.Should().NotBeNull();
        guest.Should().NotBeNull();
        admin.Should().NotBeNull();
        customer!.Delivery.Should().BeNull();
        guest!.Delivery.Should().BeNull();
        admin!.Delivery.Should().BeNull();
        customer.Items.Single().Name.Should().BeNull();
        admin.Items.Single().Name.Should().BeNull();
        admin.Fulfilment.Should().BeNull();
    }

    [Fact]
    public async Task DeliveryReads_Should_RequireTheOwningPartyOrTenantScopedGuestCapability()
    {
        var h = new BoxTestHarness();
        var orderId = await SeedOrderAsync(h, Guid.NewGuid(), new DateOnly(2026, 10, 25));
        var otherTenant = Guid.NewGuid();
        var tenant = new TestTenantProvider(otherTenant);
        var otherOrders = new StorefrontOrderService(h.Commerce(), tenant,
            new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider(), new Aonik.TestSupport.Ordering.TestOrderNumberGenerator()),
            h.GuestOrderAccess);

        (await h.StorefrontOrders().GetMyOrderAsync(Guid.NewGuid(), orderId)).Should().BeNull();
        (await h.StorefrontOrders().ListMyOrdersAsync(Guid.NewGuid())).Items.Should().BeEmpty();
        (await otherOrders.GetGuestOrderAsync(orderId, h.GuestOrderAccess.Issue(otherTenant, orderId))).Should().BeNull();
        (await AdminService(h, otherTenant).GetOrderStorefrontAsync(orderId)).Should().BeNull();
        (await AdminService(h, otherTenant).ListOrdersAsync()).Items.Should().BeEmpty();
    }

    private static async Task<Guid> SeedOrderAsync(BoxTestHarness h, Guid partyId, DateOnly? deliveryDate, OrderGiftDto? gift = null)
    {
        var tenant = new TestTenantProvider(h.TenantId);
        var orders = new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider(), new Aonik.TestSupport.Ordering.TestOrderNumberGenerator());
        var items = new List<OrderItemCommand> { new(OrderTypeCodes.ProductPurchase, 0, 95m, "GBP", Quantity: 1m, UnitPrice: 95m, Sku: "BOX") };
        if (gift?.IncludeGreetingCard == true)
            items.Add(new OrderItemCommand(CheckoutService.GreetingCardItemType, 1, 3m, "GBP", Quantity: 1m, UnitPrice: 3m, Sku: "GREETING-CARD"));
        var total = items.Sum(item => item.AmountIn);
        var order = await orders.CreateAsync(new CreateOrderCommand(OrderTypeCodes.ProductPurchase, partyId, "GBP", items));
        await using var db = h.Commerce();
        db.Carts.Add(new Cart
        {
            TenantId = h.TenantId, BuyerPartyId = partyId, OrderId = order.Id,
            AnonymousToken = CartAccess.MintToken(), Currency = "GBP", Status = CartStatuses.CheckedOut, BoxSize = 6,
        });
        db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = h.TenantId, OrderId = order.Id, Currency = "GBP", Subtotal = total, Total = total,
            PaymentIntentId = Guid.NewGuid(), PaymentStatus = CheckoutPaymentStatuses.Captured,
            GreetingCardCharged = gift?.IncludeGreetingCard == true ? 3m : 0m,
        });
        if (deliveryDate is { } date)
        {
            db.OrderDeliveryDetails.Add(new OrderDeliveryDetails
            {
                TenantId = h.TenantId, OrderId = order.Id, DeliveryDate = date, Timezone = "Europe/London",
                PurchaserEmail = "purchaser@example.com", PurchaserFirstName = "Ada", PurchaserLastName = "Cook", PurchaserPhone = "+44 20 1111 1111",
                AddressLine1 = "10 Kitchen Road", AddressLine2 = "Flat 2", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB",
                RecipientName = "Sam Recipient", RecipientPhone = "+44 20 2222 2222", Notes = "Ring the bell\nLeave with reception",
                IsGift = gift is not null, HidePrices = gift?.HidePrices ?? true,
                IncludeGreetingCard = gift?.IncludeGreetingCard ?? false, GreetingCardMessage = gift?.GreetingCardMessage,
            });
        }
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static AdminStorefrontService AdminService(BoxTestHarness h, Guid? tenantId = null)
    {
        var tenant = new TestTenantProvider(tenantId ?? h.TenantId);
        var orders = new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider(), new Aonik.TestSupport.Ordering.TestOrderNumberGenerator());
        var db = h.Commerce();
        return new AdminStorefrontService(db, tenant, orders,
            new StorefrontOrderService(h.Commerce(), tenant, orders, h.GuestOrderAccess),
            CommerceTestHarness.NewOptionService(db, h.TenantId), CommerceTestHarness.NewSelectionService(db, h.TenantId),
            h.Pricing(), NullLogger<AdminStorefrontService>.Instance, new DictionaryTenantSettingStore(h.Settings));
    }
}
