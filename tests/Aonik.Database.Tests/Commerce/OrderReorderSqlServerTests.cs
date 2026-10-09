using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Infrastructure.Multitenancy;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Commerce;

public partial class ActiveBoxConcurrencySqlServerTests
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReorderBatchRacingWithAnotherCreation_Should_CommitOneWholePartyBox(bool otherIsReorder)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var variantId = await SeedReorderDishAsync(tenantId);
        var command = new CreateBoxCartCommand(bundleId, 6, BuyerPartyId: partyId);
        ReorderBoxLine[] dishes = [new(new(variantId, 2), "Purchased dish"), new(new(variantId, 4), "Purchased dish")];
        var barrier = new PartyRangeBarrier();
        await using var first = CreateContext(tenantId, barrier);
        await using var second = CreateContext(tenantId, barrier);

        var outcomes = await Task.WhenAll(
            Capture(NewBoxes(first, tenantId).CreateFromOrderAsync(command, dishes)),
            Capture(otherIsReorder ? NewBoxes(second, tenantId).CreateFromOrderAsync(command, dishes)
                : NewBoxes(second, tenantId).CreateAsync(command with { FirstLine = new(variantId, 6) })));

        AssertOneWinner(outcomes, barrier);
        await using var verify = CreateContext(tenantId);
        var cart = await verify.Carts.Include(x => x.Items).SingleAsync();
        cart.BuyerPartyId.Should().Be(partyId); cart.RowVersion.Should().HaveCount(8);
        cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(6);
        cart.CheckoutDraftJson.Should().BeNull(); cart.OrderId.Should().BeNull();
        (await verify.InventoryReservations.AnyAsync()).Should().BeFalse();
        (await verify.CartDeliveryReservations.AnyAsync()).Should().BeFalse();
    }

    [SkippableFact]
    public async Task ReorderBatch_Should_RecognizeCommittedContents_AfterLostCommitAcknowledgement()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var variantId = await SeedReorderDishAsync(tenantId);
        var missingVariant = Guid.NewGuid();
        var interrupted = new LostCommitAcknowledgement();
        await using var context = CreateContext(tenantId, interrupted);

        var result = await NewBoxes(context, tenantId).CreateFromOrderAsync(new(bundleId, 6, BuyerPartyId: partyId),
            [new(new(variantId, 4), "Purchased dish"), new(new(missingVariant, 2), "Retired dish")]);

        interrupted.Interrupted.Should().BeTrue();
        result.Box.Lines.Should().ContainSingle().Which.Quantity.Should().Be(4);
        result.Changes.Should().ContainSingle().Which.SourceVariantId.Should().Be(missingVariant);
        result.CartToken.Should().BeNull();
        await using var verify = CreateContext(tenantId);
        var cart = await verify.Carts.Include(x => x.Items).SingleAsync();
        cart.Id.Should().Be(result.Box.CartId);
        cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(4);
        result.CartVersion.Should().Be(Convert.ToBase64String(cart.RowVersion));
    }

    [SkippableFact]
    public async Task ReorderBatch_Should_RollBackSlotFailure_AndDetachSeededLinesBeforeLaterSave()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var variantId = await SeedReorderDishAsync(tenantId);
        await using var context = CreateContext(tenantId);
        (await context.BundleSlots.SingleAsync()).MaxItems = 5;
        await context.SaveChangesAsync();
        var attempt = () => NewBoxes(context, tenantId).CreateFromOrderAsync(new(bundleId, 6, BuyerPartyId: Guid.NewGuid()),
            [new(new(variantId, 3), "Purchased dish"), new(new(variantId, 3), "Purchased dish")]);

        await attempt.Should().ThrowAsync<StorefrontValidationException>();
        await new InventoryService(context, new TestTenantProvider(tenantId), new TenantContext { TenantId = tenantId }, new WallClock())
            .SetOnHandAsync(variantId, 30m);

        await using var verify = CreateContext(tenantId);
        (await verify.Carts.AnyAsync()).Should().BeFalse();
        (await verify.CartItems.AnyAsync()).Should().BeFalse();
    }

    private async Task<Guid> SeedReorderDishAsync(Guid tenantId)
    {
        await using var context = CreateContext(tenantId);
        var product = new Product { TenantId = tenantId, Slug = "reorder-dish", Name = "Current dish",
            Kind = ProductKinds.Simple, Status = ProductStatuses.Active };
        var variant = new ProductVariant { TenantId = tenantId, ProductId = product.Id, Name = "Current dish", Sku = "DISH" };
        context.Products.Add(product); context.ProductVariants.Add(variant);
        context.InventoryLevels.Add(new InventoryLevel { TenantId = tenantId, ProductVariantId = variant.Id,
            StockItemKind = StockItemKinds.ProductVariant, OnHand = 20 });
        await context.SaveChangesAsync();
        return variant.Id;
    }
}
