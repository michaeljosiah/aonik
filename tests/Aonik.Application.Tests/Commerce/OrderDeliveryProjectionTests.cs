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
    }

    [Fact]
    public async Task DeliveryReads_Should_RequireTheOwningPartyOrTenantScopedGuestCapability()
    {
        var h = new BoxTestHarness();
        var orderId = await SeedOrderAsync(h, Guid.NewGuid(), new DateOnly(2026, 10, 25));
        var otherTenant = Guid.NewGuid();
        var tenant = new TestTenantProvider(otherTenant);
        var otherOrders = new StorefrontOrderService(h.Commerce(), tenant,
            new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider()),
            h.GuestOrderAccess);

        (await h.StorefrontOrders().GetMyOrderAsync(Guid.NewGuid(), orderId)).Should().BeNull();
        (await h.StorefrontOrders().ListMyOrdersAsync(Guid.NewGuid())).Items.Should().BeEmpty();
        (await otherOrders.GetGuestOrderAsync(orderId, h.GuestOrderAccess.Issue(otherTenant, orderId))).Should().BeNull();
        (await AdminService(h, otherTenant).GetOrderStorefrontAsync(orderId)).Should().BeNull();
        (await AdminService(h, otherTenant).ListOrdersAsync()).Items.Should().BeEmpty();
    }

    private static async Task<Guid> SeedOrderAsync(BoxTestHarness h, Guid partyId, DateOnly? deliveryDate)
    {
        var tenant = new TestTenantProvider(h.TenantId);
        var orders = new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider());
        var order = await orders.CreateAsync(new CreateOrderCommand(OrderTypeCodes.ProductPurchase, partyId, "GBP",
            [new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 95m, "GBP", Quantity: 1m, UnitPrice: 95m, Sku: "BOX")]));
        await using var db = h.Commerce();
        db.Carts.Add(new Cart
        {
            TenantId = h.TenantId, BuyerPartyId = partyId, OrderId = order.Id,
            AnonymousToken = CartAccess.MintToken(), Currency = "GBP", Status = CartStatuses.CheckedOut, BoxSize = 6,
        });
        db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = h.TenantId, OrderId = order.Id, Currency = "GBP", Subtotal = 95m, Total = 95m,
            PaymentIntentId = Guid.NewGuid(), PaymentStatus = CheckoutPaymentStatuses.Captured,
        });
        if (deliveryDate is { } date)
        {
            db.OrderDeliveryDetails.Add(new OrderDeliveryDetails
            {
                TenantId = h.TenantId, OrderId = order.Id, DeliveryDate = date, Timezone = "Europe/London",
                PurchaserEmail = "purchaser@example.com", PurchaserFirstName = "Ada", PurchaserLastName = "Cook", PurchaserPhone = "+44 20 1111 1111",
                AddressLine1 = "10 Kitchen Road", AddressLine2 = "Flat 2", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB",
                RecipientName = "Sam Recipient", RecipientPhone = "+44 20 2222 2222", Notes = "Ring the bell\nLeave with reception",
            });
        }
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static AdminStorefrontService AdminService(BoxTestHarness h, Guid? tenantId = null)
    {
        var tenant = new TestTenantProvider(tenantId ?? h.TenantId);
        var orders = new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider());
        var db = h.Commerce();
        return new AdminStorefrontService(db, tenant, orders,
            new StorefrontOrderService(h.Commerce(), tenant, orders, h.GuestOrderAccess),
            CommerceTestHarness.NewOptionService(db, h.TenantId), CommerceTestHarness.NewSelectionService(db, h.TenantId),
            h.Pricing(), NullLogger<AdminStorefrontService>.Instance);
    }
}
