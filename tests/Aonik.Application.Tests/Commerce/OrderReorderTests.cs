using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public class OrderReorderTests
{
    [Fact]
    public async Task Preview_Should_ShowPurchasedRows_AndCurrentAvailability_WithoutCreatingCart()
    {
        var source = await SeedAsync();
        await using (var edit = source.H.Commerce())
        {
            (await edit.InventoryLevels.SingleAsync(x => x.ProductVariantId == source.Fixture.DishVariants["first"])).OnHand = 0;
            await edit.SaveChangesAsync();
        }
        var preview = (await Service(source).PreviewAsync(source.Order.Id, source.PartyId))!;
        preview.Dishes.Should().HaveCount(2);
        preview.Dishes.Single(x => x.Name == "Purchased first dish").MaxQuantity.Should().Be(0);
        preview.Dishes.Single(x => x.Name == "Purchased second dish").MaxQuantity.Should().BeGreaterThan(0);
        await using var verify = source.H.Commerce();
        (await verify.Carts.CountAsync()).Should().Be(1);
        (await Service(source).PreviewAsync(source.Order.Id, Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task SelectedReorder_Should_UseOnlyChosenPurchasedRows_AndRequestedQuantity()
    {
        var source = await SeedAsync();
        await using var read = source.H.Commerce();
        var selected = await read.OrderBundleSelections.SingleAsync(x => x.ProductVariantId == source.Fixture.DishVariants["second"]);
        var result = (await Service(source).ReorderSelectedAsync(source.Order.Id, source.PartyId, [new(selected.Id, 3)]))!;
        result.Box.Size.Should().Be(6);
        result.Box.Lines.Should().ContainSingle().Which.Quantity.Should().Be(3);
        result.Box.Lines.Single().VariantId.Should().Be(selected.ProductVariantId);
        result.CheckoutDraft.Should().BeNull();
        (await read.OrderBundleSelections.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("overflow")]
    [InlineData("empty")]
    [InlineData("null")]
    public async Task SelectedReorder_Should_RejectInvalidChoices_BeforeCreatingAnyCart(string kind)
    {
        var source = await SeedAsync();
        await using var read = source.H.Commerce();
        var id = (await read.OrderBundleSelections.FirstAsync()).Id;
        IReadOnlyList<ReorderDishChoice> choices = kind switch {
            "foreign" => [new(Guid.NewGuid(), 1)], "duplicate" => [new(id, 1), new(id, 1)],
            "null" => [null!], "zero" => [new(id, 0)], "overflow" => [new(id, 100)], _ => [] };
        var attempt = () => Service(source).ReorderSelectedAsync(source.Order.Id, source.PartyId, choices);
        await attempt.Should().ThrowAsync<StorefrontValidationException>();
        (await read.Carts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Reorder_Should_RepricePurchasedDishes_AndStartWithNoCheckoutState()
    {
        var source = await SeedAsync();
        await using (var author = source.H.Commerce())
        {
            (await author.BundleSizePlans.SingleAsync()).BasePrice = 120m;
            var product = await author.Products.SingleAsync(x => x.Id == source.Fixture.DishProducts["first"]);
            product.UnitSurcharge = 2m; product.UnitSurchargeCurrency = "GBP";
            await author.SaveChangesAsync();
        }

        var result = (await Service(source).ReorderAsync(source.Order.Id, source.PartyId))!;

        result.Box.CartId.Should().NotBe(source.CartId);
        result.Box.Size.Should().Be(6);
        result.Box.Lines.Should().HaveCount(2).And.OnlyContain(x => x.LineKind == CartLineKinds.BoxDish);
        result.Box.Lines.Sum(x => x.Quantity).Should().Be(6);
        result.Quote.Total.Should().Be(124m);
        result.CheckoutDraft.Should().BeNull();
        result.OrderId.Should().BeNull();
        result.CartToken.Should().BeNull();
        result.Changes.Should().BeEmpty();
        await using var verify = source.H.Commerce();
        var fresh = await verify.Carts.SingleAsync(x => x.Id == result.Box.CartId);
        fresh.BuyerPartyId.Should().Be(source.PartyId);
        fresh.AnonymousToken.Should().BeNull();
        fresh.CheckoutState.Should().BeNull(); fresh.CheckoutPreparationJson.Should().BeNull();
        (await verify.InventoryReservations.AnyAsync()).Should().BeFalse();
        (await verify.CartDeliveryReservations.AnyAsync()).Should().BeFalse();
        var original = await verify.Carts.SingleAsync(x => x.Id == source.CartId);
        original.CheckoutDraftJson.Should().Be(source.DraftJson);
        original.CheckoutPreparationJson.Should().Be("original-payment-preparation");
        original.Status.Should().Be(CartStatuses.CheckedOut);
        (await verify.OrderBundleSelections.CountAsync()).Should().Be(2);
        source.Orders.Verify(x => x.GetAsync(source.Order.Id, It.IsAny<CancellationToken>()), Times.Once);
        source.Orders.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("off-menu")]
    [InlineData("stock")]
    public async Task Reorder_Should_SkipUnavailablePurchasedDish_WithIdentifiableNotice(string unavailable)
    {
        var source = await SeedAsync();
        await using (var author = source.H.Commerce())
        {
            var product = await author.Products.SingleAsync(x => x.Id == source.Fixture.DishProducts["first"]);
            if (unavailable == "inactive") product.Status = ProductStatuses.Draft;
            if (unavailable == "deleted") product.IsDeleted = true;
            if (unavailable == "off-menu") product.CategoryId = null;
            if (unavailable == "stock")
                (await author.InventoryLevels.SingleAsync(x => x.ProductVariantId == source.Fixture.DishVariants["first"])).OnHand = 1;
            await author.SaveChangesAsync();
        }

        var result = (await Service(source).ReorderAsync(source.Order.Id, source.PartyId))!;

        result.Box.Size.Should().Be(6);
        result.Box.Lines.Should().ContainSingle().Which.VariantId.Should().Be(source.Fixture.DishVariants["second"]);
        result.Quote.UnitsSelected.Should().Be(4);
        result.Changes.Should().ContainSingle().Which.Should().Be(new BoxChangeDto(null, null, null, null,
            unavailable == "stock" ? BoxChangeReasons.ReorderInsufficientStock : BoxChangeReasons.ReorderOffMenu,
            SourceVariantId: source.Fixture.DishVariants["first"], SourceName: "Purchased first dish"));
        var continuePartial = () => source.H.BoxCarts().ContinueAsync(result.Box.CartId,
            CartAccessContext.ForParty(source.PartyId, result.CartVersion));
        await continuePartial.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task Reorder_Should_ReportCurrentOptionRemapping_WithoutChangingPurchasedSnapshot()
    {
        var source = await SeedAsync();
        await using (var author = source.H.Commerce())
        {
            var choice = await author.OptionChoices.SingleAsync(x => x.Key == "medium");
            choice.IsActive = false;
            (await author.OptionChoices.SingleAsync(x => x.Key == "high")).IsRecommendedDefault = true;
            await author.SaveChangesAsync();
        }

        var result = (await Service(source).ReorderAsync(source.Order.Id, source.PartyId))!;

        result.Box.Lines.Should().OnlyContain(x => x.Personalisation!.Value.GetRawText().Contains("high"));
        result.Changes.Should().Contain(x => x.Group == "heat" && x.From == "medium" && x.To == "high"
            && x.SourceVariantId == source.Fixture.DishVariants["first"] && x.SourceName == "Purchased first dish");
        await using var verify = source.H.Commerce();
        (await verify.OrderBundleSelections.ToListAsync()).Should().OnlyContain(x => x.PersonalisationJson!.Contains("medium"));
    }

    [Fact]
    public async Task Reorder_Should_RejectExistingActiveBox_WithoutDiscardingIt()
    {
        var source = await SeedAsync();
        var active = await source.H.BoxCarts().CreateAsync(new(source.Fixture.BundleProductId, 6, BuyerPartyId: source.PartyId));

        var attempt = () => Service(source).ReorderAsync(source.Order.Id, source.PartyId);

        var conflict = (await attempt.Should().ThrowAsync<ActiveBoxConflictException>()).Which;
        conflict.Code.Should().Be(ActiveBoxConflictException.Existing);
        conflict.SavedCandidates.Should().ContainSingle().Which.CartId.Should().Be(active.Box.CartId);
        await using var verify = source.H.Commerce();
        (await verify.Carts.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reorder_Should_NotRevealOtherPartyOrTenantPurchase(bool otherTenant)
    {
        var source = await SeedAsync();
        var service = Service(source, tenantId: otherTenant ? Guid.NewGuid() : null);

        (await service.ReorderAsync(source.Order.Id, otherTenant ? source.PartyId : Guid.NewGuid())).Should().BeNull();

        source.Orders.VerifyNoOtherCalls();
        await using var verify = source.H.Commerce();
        (await verify.Carts.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("size")]
    [InlineData("slot")]
    public async Task Reorder_Should_RequirePaidSourceAndCurrentPlan_WithoutResizing(string invalid)
    {
        var source = await SeedAsync();
        await using (var author = source.H.Commerce())
        {
            if (invalid == "pending") (await author.OrderChargeSummaries.SingleAsync()).PaymentStatus = "RequiresAction";
            if (invalid == "size") (await author.BundleSizePlans.SingleAsync()).MinSize = 12;
            if (invalid == "slot") (await author.BundleSlots.SingleAsync()).MaxItems = 5;
            await author.SaveChangesAsync();
        }

        var attempt = () => Service(source).ReorderAsync(source.Order.Id, source.PartyId);
        await attempt.Should().ThrowAsync<StorefrontValidationException>();

        await using var verify = source.H.Commerce();
        (await verify.Carts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Reorder_Should_PropagateFailedSave_AndNotFlushRejectedLinesOnLaterWork()
    {
        var source = await SeedAsync();
        await using var context = source.H.Commerce(new RejectNewCart());
        var service = Service(source, context);
        var attempt = () => service.ReorderAsync(source.Order.Id, source.PartyId);
        await attempt.Should().ThrowAsync<DbUpdateException>().WithMessage("Test save failure");

        var tenant = new TestTenantProvider(source.H.TenantId);
        await new InventoryService(context, tenant, new TenantContext { TenantId = source.H.TenantId }, source.H.Clock)
            .SetOnHandAsync(source.Fixture.DishVariants["first"], 30m);

        await using var verify = source.H.Commerce();
        (await verify.Carts.CountAsync()).Should().Be(1);
        (await verify.CartItems.CountAsync()).Should().Be(3);
    }

    private static OrderReorderService Service(Source source, CommerceDbContext? context = null, Guid? tenantId = null)
    {
        context ??= source.H.Commerce();
        return new(context, new TestTenantProvider(tenantId ?? source.H.TenantId), source.Orders.Object, source.H.BoxCarts(context));
    }

    private static async Task<Source> SeedAsync()
    {
        var h = new BoxTestHarness();
        var fixture = await h.BuildAsync("first", "second");
        var partyId = Guid.NewGuid();
        var created = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6,
            new(fixture.DishVariants["first"], 2), BuyerPartyId: partyId));
        var access = CartAccessContext.ForParty(partyId, created.CartVersion);
        var filled = await h.BoxCarts().AddLineAsync(created.Box.CartId, new(fixture.DishVariants["second"], 4), access);
        var extra = await h.AddExtraAsync("old-extra", 8m);
        await h.BoxCarts().AddExtraLineAsync(created.Box.CartId, new(extra.VariantId, 1), access with { ExpectedCartVersion = filled.CartVersion });
        var orderId = Guid.NewGuid();
        var order = new OrderDto(orderId, h.TenantId, OrderTypeCodes.ProductPurchase, OrderStatusCodes.Complete,
            partyId, 106m, "GBP", h.Clock.UtcNow,
            [new OrderItemDto(Guid.NewGuid(), OrderTypeCodes.ProductPurchase, 0, "Complete", 95m, "GBP", null, 1m, 95m,
                fixture.BundleProductId, "meal-box", "{}")]);
        var draft = JsonSerializer.Serialize(new CartCheckoutDraftDto(BoxTestHarness.ValidDelivery.Purchaser,
            BoxTestHarness.ValidDelivery.Address, DeliveryDate: BoxTestHarness.ValidDelivery.DeliveryDate,
            Gift: new(true, IncludeGreetingCard: true, GreetingCardMessage: "Old message"), CreateAccount: true, DiscountCode: "OLD"));
        await using var db = h.Commerce();
        var cart = await db.Carts.Include(x => x.Items).SingleAsync();
        cart.Status = CartStatuses.CheckedOut; cart.OrderId = orderId;
        cart.CheckoutDraftJson = draft; cart.CheckoutPreparationJson = "original-payment-preparation";
        db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            TenantId = h.TenantId, OrderId = orderId, Currency = "GBP", Subtotal = 106m, Total = 106m, PaymentStatus = "Captured"
        });
        foreach (var item in cart.Items.Where(x => x.LineKind == CartLineKinds.BoxDish))
            db.OrderBundleSelections.Add(new OrderBundleSelection
            {
                TenantId = h.TenantId, OrderId = orderId, OrderItemIndex = 0, BundleSlotId = item.BoxBundleSlotId!.Value,
                ProductVariantId = item.ProductVariantId, Quantity = item.Quantity, Sku = item.Sku,
                PersonalisationJson = item.PersonalisationJson, NameSnapshot = item.ProductVariantId == fixture.DishVariants["first"]
                    ? "Purchased first dish" : "Purchased second dish"
            });
        await db.SaveChangesAsync();
        var orders = new Mock<IOrderService>(MockBehavior.Strict);
        orders.Setup(x => x.GetAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return new(h, fixture, partyId, created.Box.CartId, order, orders, draft);
    }

    private sealed record Source(BoxTestHarness H, BoxTestHarness.BoxFixture Fixture, Guid PartyId, Guid CartId,
        OrderDto Order, Mock<IOrderService> Orders, string DraftJson);

    private sealed class RejectNewCart : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Cart>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Test save failure");
            return ValueTask.FromResult(result);
        }
    }
}
