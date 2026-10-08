using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Services.Inventory;
using Aonik.SharedKernel.Abstractions.Ordering;

using FluentAssertions;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public partial class ProductionPlanningServiceTests
{
    private static readonly DateOnly DeliveryDate = new(2026, 7, 10);

    [Fact]
    public async Task DeliverySheet_Should_UseRecordedDate_AndRetainTenantTypeAndStatusBoundaries()
    {
        var h = new Harness();
        var dish = await h.SeedVariantAsync("Jollof", "Regular");
        var earlier = await h.CreateProductPurchaseAsync(FromUtc.AddMonths(-1), OrderStatusCodes.Complete, (dish, 2m));
        var pending = await h.CreateProductPurchaseAsync(ToUtc.AddDays(1), OrderStatusCodes.Pending, (dish, 3m));
        var otherDate = await h.CreateProductPurchaseAsync(FromUtc, OrderStatusCodes.Complete, (dish, 20m));
        var draft = await h.CreateProductPurchaseAsync(FromUtc, null, (dish, 30m));
        var cancelled = await h.CreateProductPurchaseAsync(FromUtc, OrderStatusCodes.Cancelled, (dish, 40m));
        var foreignSnapshot = await h.CreateProductPurchaseAsync(FromUtc, OrderStatusCodes.Complete, (dish, 50m));
        await h.CreateProductPurchaseAsync(FromUtc, OrderStatusCodes.Complete, (dish, 60m)); // legacy, no date
        var otherType = await h.Orders().CreateAsync(new CreateOrderCommand(
            OrderTypeCodes.BillPayment, null, "GBP",
            [new OrderItemCommand("BillPayment", 0, 70m, "GBP", ProductId: dish, Quantity: 70m)]));
        await h.Orders().TransitionAsync(otherType.Id, OrderStatusCodes.Complete);
        await using (var db = h.Commerce())
        {
            db.OrderDeliveryDetails.AddRange(
                DeliverySnapshot(h.TenantId, earlier.Id), DeliverySnapshot(h.TenantId, pending.Id),
                DeliverySnapshot(h.TenantId, otherDate.Id, DeliveryDate.AddDays(1)),
                DeliverySnapshot(h.TenantId, draft.Id), DeliverySnapshot(h.TenantId, cancelled.Id),
                DeliverySnapshot(Guid.NewGuid(), foreignSnapshot.Id), DeliverySnapshot(h.TenantId, otherType.Id));
            await db.SaveChangesAsync();
        }

        var sheet = await h.Planning().GetProductionSheetForDeliveryDateAsync(DeliveryDate);

        sheet.Window.Should().BeNull();
        sheet.DeliveryDate.Should().Be(DeliveryDate);
        sheet.TotalOrders.Should().Be(2);
        sheet.Lines.Should().ContainSingle().Which.PortionsDemanded.Should().Be(5m);
    }

    [Fact]
    public async Task DeliverySheet_Should_NotQueryUnrestrictedOrders_WhenNoSnapshotsMatch()
    {
        var h = new Harness();
        var orders = new Mock<IOrderService>(MockBehavior.Strict);

        var sheet = await h.Planning(orders.Object).GetProductionSheetForDeliveryDateAsync(DeliveryDate);

        sheet.TotalOrders.Should().Be(0);
        sheet.Lines.Should().BeEmpty();
        orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeliverySheet_Should_IncludeEveryOrder_WhenTheDayExceedsOneSpinePage()
    {
        var h = new Harness();
        var dish = await h.SeedVariantAsync("Jollof", "Regular");
        var snapshots = new List<OrderDeliveryDetails>();
        for (var index = 0; index < 201; index++)
        {
            var order = await h.CreateProductPurchaseAsync(FromUtc.AddDays(-1), OrderStatusCodes.Pending, (dish, 1m));
            snapshots.Add(DeliverySnapshot(h.TenantId, order.Id));
        }
        await using (var db = h.Commerce())
        {
            db.OrderDeliveryDetails.AddRange(snapshots);
            await db.SaveChangesAsync();
        }

        var sheet = await h.Planning().GetProductionSheetForDeliveryDateAsync(DeliveryDate);

        sheet.TotalOrders.Should().Be(201);
        var line = sheet.Lines.Should().ContainSingle().Subject;
        line.PortionsDemanded.Should().Be(201m);
        line.OrderCount.Should().Be(201);
    }

    [Fact]
    public async Task DeliveryPrepList_Should_ReuseBundleExpansionRecipesAndAvailableStock()
    {
        var h = new Harness();
        var dish = await h.SeedVariantAsync("Jollof", "Regular");
        var rice = await h.SeedIngredientAsync("Rice");
        await h.Recipes().SetRecipeAsync(new SetRecipeCommand(dish, "Jollof", 4m, "portion",
            [new RecipeComponentCommand(rice, 1m)]));
        var order = await h.CreateProductPurchaseAsync(FromUtc.AddMonths(-1), OrderStatusCodes.Complete, (Guid.NewGuid(), 1m));
        await h.SeedBundleSelectionsAsync(order.Id, 0, (dish, 8m));
        await using (var db = h.Commerce())
        {
            db.OrderDeliveryDetails.Add(DeliverySnapshot(h.TenantId, order.Id));
            await db.SaveChangesAsync();
        }
        await h.Inventory().SetOnHandAsync(StockItemRef.Ingredient(rice), 4m);
        await h.Inventory().ReserveAsync(Guid.NewGuid(), [new InventoryReservationLine(StockItemRef.Ingredient(rice), 3m)]);

        var prep = await h.Planning().GetPrepListForDeliveryDateAsync(DeliveryDate);

        prep.Window.Should().BeNull();
        prep.DeliveryDate.Should().Be(DeliveryDate);
        prep.NettedAgainstStock.Should().BeTrue();
        var line = prep.Lines.Should().ContainSingle().Subject;
        line.RequiredQuantity.Should().Be(2m);
        line.Available.Should().Be(1m);
        line.Shortfall.Should().Be(1m);
    }

    private static OrderDeliveryDetails DeliverySnapshot(Guid tenantId, Guid orderId, DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, OrderId = orderId,
        DeliveryDate = date ?? DeliveryDate, Timezone = "Europe/London",
        PurchaserEmail = "buyer@example.com", PurchaserFirstName = "Test", PurchaserLastName = "Buyer",
        PurchaserPhone = "07700900123", RecipientName = "Test Buyer", RecipientPhone = "07700900123",
        AddressLine1 = "1 Test Road", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB",
    };
}
