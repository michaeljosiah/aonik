using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Catalog;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public partial class ProductionOrderServiceTests
{
    [Fact]
    public async Task CreateFromDeliverySheet_Should_UseScheduledDemandAndExplicitCookingTime()
    {
        var h = new Harness();
        var (dish, _, _) = await h.SeedJollofAsync();
        var deliveryDate = new DateOnly(2026, 7, 10);
        var orderId = await h.CreateDemandOrderAsync(FromUtc.AddMonths(-1), (dish, 8m));
        await h.CreateDemandOrderAsync(FromUtc, (dish, 100m)); // no recorded delivery date
        await using (var db = h.Commerce())
        {
            db.OrderDeliveryDetails.Add(new OrderDeliveryDetails
            {
                Id = Guid.NewGuid(), TenantId = h.TenantId, OrderId = orderId,
                DeliveryDate = deliveryDate, Timezone = "Europe/London",
                PurchaserEmail = "buyer@example.com", PurchaserFirstName = "Test", PurchaserLastName = "Buyer",
                PurchaserPhone = "07700900123", RecipientName = "Test Buyer", RecipientPhone = "07700900123",
                AddressLine1 = "1 Test Road", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB",
            });
            await db.SaveChangesAsync();
        }

        var result = await h.ProductionOrders().CreateFromProductionSheetAsync(
            new CreateFromProductionSheetCommand(PlannedFor: PlannedFor, DeliveryDate: deliveryDate));

        result.Order.PlannedFor.Should().Be(PlannedFor);
        result.Order.Lines.Should().ContainSingle().Which.PlannedQuantity.Should().Be(8m);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("from-only")]
    [InlineData("to-only")]
    [InlineData("mixed")]
    [InlineData("delivery-without-cooking-time")]
    public async Task CreateFromDeliverySheet_Should_RejectIncompleteOrAmbiguousSelection(string scenario)
    {
        var h = new Harness();
        var date = new DateOnly(2026, 7, 10);
        var command = scenario switch
        {
            "from-only" => new CreateFromProductionSheetCommand(FromUtc: FromUtc),
            "to-only" => new CreateFromProductionSheetCommand(ToUtc: ToUtc),
            "mixed" => new CreateFromProductionSheetCommand(FromUtc, ToUtc, PlannedFor, DeliveryDate: date),
            "delivery-without-cooking-time" => new CreateFromProductionSheetCommand(DeliveryDate: date),
            _ => new CreateFromProductionSheetCommand(),
        };

        var act = () => h.ProductionOrders().CreateFromProductionSheetAsync(command);

        await act.Should().ThrowAsync<StorefrontValidationException>();
        await using var db = h.Commerce();
        (await db.ProductionOrders.CountAsync()).Should().Be(0);
    }
}
