using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 071 — add-on lines: extras alongside the box at their own retail price, outside every
/// capacity/fullness rule (X3), inside availability (X5), pricing (X2) and the quote (X6).
/// </summary>
public class BoxAddOnTests
{
    private static JsonElement Sel(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CartAccessContext Token(BoxCartDto dto) => CartAccessContext.ForGuest(dto.CartToken);

    private static async Task<(BoxTestHarness H, BoxTestHarness.BoxFixture F, BoxCartDto Box, Guid ExtraVariant)> ArrangeAsync()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var (_, extraVariant) = await h.AddExtraAsync("pepper-sauce", 3.50m);
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        return (h, f, box, extraVariant);
    }

    [Fact]
    public async Task B1_B2_AddOns_NeitherConsumeSpace_NorSatisfyTheGate()
    {
        var (h, f, box, extraVariant) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 6, null), access);
        var withExtras = await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 2), access);

        withExtras.Quote.UnitsSelected.Should().Be(6, "B1 — a full box still accepts extras");
        withExtras.Quote.IsFull.Should().BeTrue();
        await carts.ContinueAsync(box.Box.CartId, access);

        // B2 — extras never satisfy the gate: a fresh box with only extras cannot continue.
        var second = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        await carts.AddExtraLineAsync(second.Box.CartId, new AddBoxExtraCommand(extraVariant, 3), Token(second));
        var gate = () => carts.ContinueAsync(second.Box.CartId, Token(second));
        (await gate.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R8");
    }

    [Fact]
    public async Task B3_TheAddOnsComponent_JoinsTheSum()
    {
        var (h, _, box, extraVariant) = await ArrangeAsync();

        var after = await h.BoxCarts().AddExtraLineAsync(box.Box.CartId,
            new AddBoxExtraCommand(extraVariant, 2), Token(box));

        after.Quote.Components.Single(c => c.Key == "addOns").Amount.Should().Be(7.00m, "3.50 × 2");
        after.Quote.Total.Should().Be(after.Quote.Components.Sum(c => c.Amount), "A24/X6");
        var line = after.Box.Lines.Single(l => l.LineKind == "AddOn");
        line.UnitPrice.Should().Be(3.50m, "add-ons are ordinary retail and show their price");
    }

    [Fact]
    public async Task B4_UnpriceableExtras_RejectOnAdd_AndFlagOnLoad()
    {
        var (h, _, box, extraVariant) = await ArrangeAsync();
        var (_, unpriced) = await h.AddExtraAsync("mystery-jar", price: 0m);
        var carts = h.BoxCarts();
        var access = Token(box);

        var add = () => carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(unpriced, 1), access);
        (await add.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("X2");

        // Price removed AFTER adding: the line stays, flagged, blocking the gate (X2 via drift).
        await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 1), access);
        await using (var ctx = h.Commerce())
        {
            var price = await ctx.ProductPrices.FirstAsync(p => p.ProductVariantId == extraVariant);
            ctx.ProductPrices.Remove(price);
            await ctx.SaveChangesAsync();
        }
        var flagged = await carts.GetAsync(box.Box.CartId, access);
        flagged.Box.Lines.Single(l => l.LineKind == "AddOn").IsUnavailable.Should().BeTrue();
        flagged.Changes.Should().Contain(c => c.Reason == "unavailable");
    }

    [Fact]
    public async Task B5_AddOns_MergeAndSplit_LikeAnyLine()
    {
        var (h, _, box, extraVariant) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 1), access);
        var merged = await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 1), access);
        merged.Box.Lines.Where(l => l.LineKind == "AddOn").Should().HaveCount(1);
        merged.Box.Lines.Single(l => l.LineKind == "AddOn").Quantity.Should().Be(2);
    }

    [Fact]
    public async Task B7_Availability_SumsDishAndAddOnDemand()
    {
        // The same variant drawn as a dish AND an add-on shares one stock pool (X5).
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var (_, _) = await h.AddExtraAsync("pepper-sauce", 3.50m);
        // Make the jollof variant itself an extra too, so both kinds draw it.
        await using (var ctx = h.Commerce())
        {
            var collection = await ctx.Collections.FirstAsync(c => c.Slug == "extras");
            ctx.CollectionItems.Add(new Aonik.Commerce.Entities.Catalog.CollectionItem
            {
                Id = Guid.NewGuid(), TenantId = h.TenantId, CollectionId = collection.Id,
                ProductId = f.DishProducts["jollof"], Rank = 5,
            });
            await ctx.SaveChangesAsync();
        }
        await h.Pricing().SetPriceAsync(new Aonik.Commerce.Contracts.Models.Catalog.SetPriceCommand(
            f.DishVariants["jollof"], "GBP", 6m));
        await h.Inventory().SetOnHandAsync(f.DishVariants["jollof"], 4m);

        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        var carts = h.BoxCarts();
        var access = Token(box);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 3, null), access);

        var creep = () => carts.AddExtraLineAsync(box.Box.CartId,
            new AddBoxExtraCommand(f.DishVariants["jollof"], 2), access);
        (await creep.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R5");
    }

    [Fact]
    public async Task B6_MixedCheckout_MaterialisesRetailItems_AndCombinedGoods()
    {
        var (h, f, box, extraVariant) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 6, null), access);
        await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 2), access);

        var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), access);

        // Goods: box 95 + extras 7 = 102 (X7).
        result.Total.Should().Be(102m);
        h.Payments.LastAmount.Should().Be(102m);

        await using var ordering = h.Ordering();
        var order = await ordering.Orders.Include(o => o.Items).FirstAsync(o => o.Id == result.OrderId);
        order.Items.Should().HaveCount(2, "the box item + one retail item per AddOn line");
        var boxItem = order.Items.Single(i => i.ItemIndex == 0);
        boxItem.UnitPrice.Should().Be(95m, "the box item never absorbs an add-on amount");
        var extraItem = order.Items.Single(i => i.ItemIndex == 1);
        extraItem.Quantity.Should().Be(2m);
        extraItem.UnitPrice.Should().Be(3.50m);
        extraItem.ProductId.Should().Be(extraVariant);

        await using var commerce = h.Commerce();
        var selections = await commerce.OrderBundleSelections.Where(s => s.OrderId == result.OrderId).ToListAsync();
        selections.Should().OnlyContain(s => s.ProductVariantId == f.DishVariants["jollof"],
            "selection rows are the BOX'S kitchen landing — dishes only");
    }

    [Fact]
    public async Task B8_ExtrasRead_ServesPricedRows_AndCountsSkips()
    {
        var h = new BoxTestHarness();
        await h.BuildAsync("jollof");
        await h.AddExtraAsync("pepper-sauce", 3.50m);
        await h.AddExtraAsync("mystery-jar", price: 0m);   // member, unpriceable

        var list = await h.Extras().GetExtrasAsync();

        list.Rows.Should().HaveCount(1);
        list.Rows.Single().Name.Should().Be("pepper-sauce");
        list.Rows.Single().UnitPrice.Should().Be(3.50m);
        list.Skipped.Should().Be(1, "unpriceable members count, never silently drop");

        // Unconfigured slug (no matching collection) → empty, never a guess.
        h.Settings["Commerce.Storefront.ExtrasCollectionSlug"] = "no-such-collection";
        (await h.Extras().GetExtrasAsync()).Rows.Should().BeEmpty();
    }
}
