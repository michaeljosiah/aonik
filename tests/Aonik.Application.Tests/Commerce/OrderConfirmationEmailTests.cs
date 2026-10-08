using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Commerce;

public class OrderConfirmationEmailTests
{
    [Fact]
    public async Task SendAsync_Should_UseRecordedRecipientAndCharges_WithoutDraftOrPaymentSecrets()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Cart.CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(
            Purchaser: new CheckoutContactDto("later@example.test", "Later", "Buyer", "07000000000")));
        await fixture.Context.SaveChangesAsync();

        await fixture.Service().SendAsync(fixture.OrderId, fixture.PaymentId);

        var sent = fixture.Sent.Should().ContainSingle().Subject;
        sent.TemplateName.Should().Be(TransactionalEmailTemplateNames.OrderConfirmation);
        sent.To.Should().Be("purchaser@example.test");
        sent.Model["order_id"].Should().Be(fixture.OrderId.ToString("D"));
        sent.Model["total"].Should().Be("91.1234");
        sent.Model["subtotal"].Should().Be("100.1234");
        sent.Model["delivery_total"].Should().Be("7.00");
        var items = ((IEnumerable<Dictionary<string, object?>>)sent.Model["items"]!).ToList();
        items.Should().ContainSingle();
        items[0]["description"].Should().Be("BOX-SNAPSHOT");
        items[0]["total"].Should().Be("100.1234");
        var delivery = (Dictionary<string, object?>)sent.Model["delivery"]!;
        delivery["date"].Should().Be("2026-11-01");
        delivery["line1"].Should().Be("10 Kitchen Road");
        delivery["recipient_name"].Should().Be("Recipient Snapshot");
        var selection = ((IEnumerable<Dictionary<string, object?>>)sent.Model["selections"]!).Single();
        selection["description"].Should().Be("DISH-SNAPSHOT");
        selection["personalisation"].Should().Be("Full table · Salmon");
        var payload = JsonSerializer.Serialize(sent.Model);
        payload.Should().NotContain("later@example.test").And.NotContain("payment-client-secret")
            .And.NotContain("https://provider.example").And.NotContain("raw-private-metadata")
            .And.NotContain("personalisation-private-json");
    }

    [Theory]
    [InlineData("Pending", CartStatuses.CheckedOut, OrderStatusCodes.Complete, true)]
    [InlineData("Refunded", CartStatuses.CheckedOut, OrderStatusCodes.Complete, true)]
    [InlineData(CheckoutPaymentStatuses.Captured, CartStatuses.Open, OrderStatusCodes.Complete, true)]
    [InlineData(CheckoutPaymentStatuses.Captured, CartStatuses.CheckedOut, OrderStatusCodes.Draft, true)]
    [InlineData(CheckoutPaymentStatuses.Captured, CartStatuses.CheckedOut, OrderStatusCodes.Cancelled, true)]
    [InlineData(CheckoutPaymentStatuses.Captured, CartStatuses.CheckedOut, OrderStatusCodes.Complete, false)]
    public async Task SendAsync_Should_NotSendUntilTheRecordedPaymentAndOrderAreComplete(
        string paymentStatus, string cartStatus, string orderStatus, bool matchingIntent)
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Summary.PaymentStatus = paymentStatus;
        fixture.Cart.Status = cartStatus;
        fixture.Order = fixture.Order with { Status = orderStatus };
        await fixture.Context.SaveChangesAsync();

        await fixture.Service().SendAsync(fixture.OrderId, matchingIntent ? fixture.PaymentId : Guid.NewGuid());

        fixture.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_Should_NotReadAnotherTenantsContact()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var otherTenant = Guid.NewGuid();
        await using var otherContext = new CommerceDbContext(fixture.Options, new TestTenantProvider(otherTenant));
        var service = new OrderConfirmationEmailService(otherContext, new TestTenantProvider(otherTenant),
            fixture.Orders.Object, fixture.Email.Object, NullLogger<OrderConfirmationEmailService>.Instance);

        await service.SendAsync(fixture.OrderId, fixture.PaymentId);

        fixture.Sent.Should().BeEmpty();
        fixture.Orders.Verify(service => service.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_RetryMissingBoxContact_ButSkipLegacyGenericOrdersWithoutContact()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Context.OrderDeliveryDetails.RemoveRange(fixture.Context.OrderDeliveryDetails);
        await fixture.Context.SaveChangesAsync();
        var service = fixture.Service();

        var send = () => service.SendAsync(fixture.OrderId, fixture.PaymentId);
        await send.Should().ThrowAsync<InvalidOperationException>().WithMessage("*contact snapshot*");
        fixture.Cart.BoxBundleProductId = null;
        await fixture.Context.SaveChangesAsync();
        await send();

        fixture.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_Should_PropagateProviderFailure_WithoutChangingRecordedOrderFacts()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        fixture.Email.Setup(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider temporarily unavailable"));

        var send = () => fixture.Service().SendAsync(fixture.OrderId, fixture.PaymentId);
        await send.Should().ThrowAsync<InvalidOperationException>().WithMessage("Provider temporarily unavailable");

        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
        fixture.Summary.PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        fixture.Cart.Status.Should().Be(CartStatuses.CheckedOut);
        fixture.Orders.Verify(service => service.TransitionAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();
        public Guid PaymentId { get; } = Guid.NewGuid();
        public DbContextOptions<CommerceDbContext> Options { get; } = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options;
        public CommerceDbContext Context { get; }
        public Mock<IOrderService> Orders { get; } = new();
        public Mock<ITemplatedEmailSender> Email { get; } = new();
        public List<TemplatedEmailMessage> Sent { get; } = [];
        public Cart Cart { get; }
        public OrderChargeSummary Summary { get; }
        public OrderDto Order { get; set; }

        public Fixture()
        {
            Context = new CommerceDbContext(Options, new TestTenantProvider(TenantId));
            Cart = new Cart { TenantId = TenantId, OrderId = OrderId, Currency = "GBP",
                BoxBundleProductId = Guid.NewGuid(), Status = CartStatuses.CheckedOut };
            Summary = new OrderChargeSummary
            {
                TenantId = TenantId, OrderId = OrderId, PaymentIntentId = PaymentId,
                Currency = "GBP", Subtotal = 100.1234m, DiscountTotal = 20m, TaxTotal = 4m, Total = 91.1234m,
                PaymentStatus = CheckoutPaymentStatuses.Captured, PaymentClientSecret = "payment-client-secret",
                PaymentCheckoutUrl = "https://provider.example/private"
            };
            Order = new OrderDto(OrderId, TenantId, OrderTypeCodes.ProductPurchase, OrderStatusCodes.Complete,
                null, 107.1234m, "GBP", DateTime.UtcNow,
                [new OrderItemDto(Guid.NewGuid(), OrderTypeCodes.ProductPurchase, 0, "Draft", 100.1234m, "GBP",
                    null, 1, 100.1234m, Guid.NewGuid(), "BOX-SNAPSHOT", "raw-private-metadata"),
                 new OrderItemDto(Guid.NewGuid(), CheckoutService.DeliveryFeeItemType, 1, "Draft", 7, "GBP",
                    null, 1, 7, null, "delivery", "{}")]);
            Orders.Setup(service => service.GetAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(() => Order);
            Email.Setup(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()))
                .Callback<TemplatedEmailMessage, CancellationToken>((message, _) => Sent.Add(message))
                .Returns(Task.CompletedTask);
        }

        public async Task SeedAsync()
        {
            Context.Carts.Add(Cart);
            Context.OrderChargeSummaries.Add(Summary);
            Context.OrderDeliveryDetails.Add(new OrderDeliveryDetails
            {
                TenantId = TenantId, OrderId = OrderId, PurchaserEmail = "purchaser@example.test",
                PurchaserFirstName = "Purchaser", PurchaserLastName = "Snapshot", PurchaserPhone = "07000000000",
                AddressLine1 = "10 Kitchen Road", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB",
                DeliveryDate = new DateOnly(2026, 11, 1), Timezone = "Europe/London",
                RecipientName = "Recipient Snapshot", RecipientPhone = "07000000001", Notes = "Ring bell"
            });
            Context.OrderBundleSelections.Add(new OrderBundleSelection
            {
                TenantId = TenantId, OrderId = OrderId, Sku = "DISH-SNAPSHOT", Quantity = 6,
                BundleSlotId = Guid.NewGuid(), ProductVariantId = Guid.NewGuid(),
                PersonalisationSummary = "Full table · Salmon", PersonalisationJson = "personalisation-private-json"
            });
            await Context.SaveChangesAsync();
        }

        public OrderConfirmationEmailService Service() => new(Context, new TestTenantProvider(TenantId),
            Orders.Object, Email.Object, NullLogger<OrderConfirmationEmailService>.Instance);

        public void Dispose() => Context.Dispose();
    }
}
