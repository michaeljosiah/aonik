using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Ordering.Services;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Spec 073 dependency endpoints — the admin storefront projections
/// (083 order list/detail + carts, 081 party summary) and the raw authoring
/// reads (074 narrowing, 075 content, 076 draft-bundle plan, 078 extras
/// pricing state). Everything is read-only over what checkout persisted.</summary>
public class AdminStorefrontProjectionTests
{
    private static AdminStorefrontService AdminSvc(BoxTestHarness h)
    {
        var tenant = new TestTenantProvider(h.TenantId);
        var spine = new CoreOrderService(h.Ordering(), tenant, new CommerceTestHarness.TestClock(), new TestCurrentUserProvider());
        return new AdminStorefrontService(
            h.Commerce(), tenant, spine,
            new StorefrontOrderService(h.Commerce(), tenant, spine),
            h.Pricing());
    }

    [Fact]
    public async Task OrderProjections_CarryPaymentBuyerAndKitchenFacts()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var (_, extraVariant) = await h.AddExtraAsync("puff-puff", 4.50m);
        var party = Guid.NewGuid();

        var carts = h.BoxCarts();
        var box = await carts.CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6, BuyerPartyId: party));
        var access = CartAccessContext.ForParty(party);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 6, null), access);
        await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 2), access);
        var checkout = await h.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), access);

        var admin = AdminSvc(h);

        // D8 — the list projection: buyer kind, payment status, box size, charge total.
        var list = await admin.ListOrdersAsync();
        list.TotalCount.Should().Be(1);
        var row = list.Items.Single();
        row.OrderId.Should().Be(checkout.OrderId);
        row.BuyerKind.Should().Be("party");
        row.BuyerPartyId.Should().Be(party);
        row.BoxSize.Should().Be(6);
        row.PaymentStatus.Should().NotBeNullOrWhiteSpace();
        row.Total.Should().Be(95m + 9m, "the box goods total plus two £4.50 add-ons");

        // D6 — the full storefront detail: aggregate vs add-on separation, the
        // kitchen landing, and a charge envelope that matches the list row.
        var detail = await admin.GetOrderStorefrontAsync(checkout.OrderId);
        detail.Should().NotBeNull();
        var aggregate = detail!.Items.Single(i => !i.IsAddOn && !i.IsDeliveryFee);
        aggregate.Amount.Should().Be(95m);
        aggregate.Quantity.Should().Be(1m);
        var addOn = detail.Items.Single(i => i.IsAddOn);
        addOn.Amount.Should().Be(9m);
        addOn.UnitPrice.Should().Be(4.50m);
        detail.Selections.Should().ContainSingle().Which.Quantity.Should().Be(6m);
        detail.Charge.Total.Should().Be(row.Total);
        detail.PaymentStatus.Should().Be(row.PaymentStatus);
        detail.FulfilmentStatus.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CartsAdmin_ListsBoxMeta_AndComputesDetailFlagsReadOnly()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var (_, extraVariant) = await h.AddExtraAsync("zobo", 3.50m);

        var carts = h.BoxCarts();
        var box = await carts.CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        var access = CartAccessContext.ForGuest(box.CartToken);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 4, null), access);
        await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(extraVariant, 1), access);

        var admin = AdminSvc(h);

        var list = await admin.ListCartsAsync();
        var row = list.Items.Single(r => r.CartId == box.Box.CartId);
        row.BuyerKind.Should().Be("guest");
        row.BoxMeta.Should().NotBeNull();
        row.BoxMeta!.Size.Should().Be(6);
        row.BoxMeta.Filled.Should().Be(4, "add-ons never count toward box fullness");
        row.ItemCount.Should().Be(5m);

        var before = await admin.GetCartAsync(box.Box.CartId);
        before!.Lines.Should().OnlyContain(l => !l.IsUnavailable && !l.PriceChanged);

        // Shrink stock below the demanded 4 and bump the add-on's retail price —
        // the detail must FLAG both without persisting anything.
        await h.Inventory().SetOnHandAsync(f.DishVariants["jollof"], 2m);
        await using (var ctx = h.Commerce())
        {
            var price = await ctx.ProductPrices.FirstAsync(p => p.ProductVariantId == extraVariant);
            price.Amount += 1m;
            await ctx.SaveChangesAsync();
        }

        var after = await admin.GetCartAsync(box.Box.CartId);
        after!.Lines.Single(l => l.Kind == "BoxDish").IsUnavailable.Should().BeTrue();
        after.Lines.Single(l => l.Kind == "AddOn").PriceChanged.Should().BeTrue();

        // Read-only proof: the stored add-on snapshot is untouched — the flag was
        // computed against the live price, not written back (repair stays the
        // customer load path's job).
        await using var verify = h.Commerce();
        (await verify.CartItems.SingleAsync(i => i.CartId == box.Box.CartId && i.LineKind == "AddOn"))
            .UnitPriceSnapshot.Should().Be(3.50m);
    }

    [Fact]
    public async Task PartyStorefront_ReportsRecordedAdoptionFactAndActiveCart()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var admin = AdminSvc(h);

        // Adopted party: guest-built box whose token AdoptAsync retires.
        var adoptedParty = Guid.NewGuid();
        var guestBox = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        await h.BoxCarts().AddLineAsync(guestBox.Box.CartId,
            new AddBoxLineCommand(f.DishVariants["jollof"], 2, null), CartAccessContext.ForGuest(guestBox.CartToken));
        await h.Carts().AdoptAsync(guestBox.Box.CartId, adoptedParty, CartAccessContext.ForGuest(guestBox.CartToken));

        var adopted = await admin.GetPartyStorefrontAsync(adoptedParty);
        adopted.Adopted.Should().BeTrue("the party-bound cart's guest token was retired");
        adopted.ActiveCart.Should().NotBeNull();
        adopted.ActiveCart!.Size.Should().Be(6);
        adopted.ActiveCart.Filled.Should().Be(2);
        adopted.Orders.Should().BeEmpty();

        // Born-bound party: created signed-in, token minted but never retired.
        var bornParty = Guid.NewGuid();
        await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 8, BuyerPartyId: bornParty));

        var born = await admin.GetPartyStorefrontAsync(bornParty);
        born.Adopted.Should().BeFalse("a born-bound cart keeps its minted token — no adoption ever happened");
        born.ActiveCart.Should().NotBeNull();
        born.ActiveCart!.Size.Should().Be(8);
    }

    [Fact]
    public async Task Narrowing_RawRead_PreservesNullVersusExplicit()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var product = f.DishProducts["jollof"];
        var ctx = h.Commerce();
        var options = CommerceTestHarness.NewOptionService(ctx, h.TenantId);

        var inherit = await options.CreateGroupAsync(new CreateOptionGroupCommand("wrap", "Wrapping"));
        await options.AddChoiceAsync(inherit.Id, new AddOptionChoiceCommand("paper", "Paper", IsRecommendedDefault: true));
        await options.AddChoiceAsync(inherit.Id, new AddOptionChoiceCommand("foil", "Foil"));
        var pinned = await options.CreateGroupAsync(new CreateOptionGroupCommand("sauce", "Sauce"));
        await options.AddChoiceAsync(pinned.Id, new AddOptionChoiceCommand("mild", "Mild", IsRecommendedDefault: true));
        await options.AddChoiceAsync(pinned.Id, new AddOptionChoiceCommand("hot", "Hot"));

        await options.SetProductOptionGroupsAsync(product, new SetProductOptionGroupsCommand(
        [
            new ProductOptionGroupLine("wrap", AllowedChoiceKeys: null, SortOrder: 0),
            new ProductOptionGroupLine("sauce", AllowedChoiceKeys: ["mild"], SortOrder: 1),
        ]));

        var raw = await options.GetNarrowingAsync(product);
        raw.Should().HaveCount(2);
        raw.Single(l => l.GroupKey == "wrap").AllowedChoiceKeys.Should().BeNull(
            "inherited-null must survive the round trip — pinning it silently would freeze future choices out");
        raw.Single(l => l.GroupKey == "sauce").AllowedChoiceKeys.Should().BeEquivalentTo(["mild"]);
    }

    [Fact]
    public async Task ContentAdmin_ComputesStaleness_AndListsStatusFlags()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var product = f.DishProducts["jollof"];
        var ctx = h.Commerce();
        var content = new ProductContentService(
            ctx, new TestTenantProvider(h.TenantId),
            CommerceTestHarness.NewSelectionService(ctx, h.TenantId),
            CommerceTestHarness.NewOptionService(ctx, h.TenantId));

        await content.UpsertContentAsync(product, new UpsertProductContentCommand("Per serving", Kcal: 400));

        var fresh = await content.GetAdminAsync(product);
        fresh.Block.Should().NotBeNull();
        fresh.IsStale.Should().BeFalse();

        // Simulate a default-combination drift the flag write missed: the stored
        // binding no longer matches the current all-defaults selection. The
        // resolver withholds in this state, so the admin read must call it stale.
        await using (var mutate = h.Commerce())
        {
            var block = await mutate.ProductContents.FirstAsync(c => c.ProductId == product);
            block.DescribesSelectionJson = """{"ghost":"binding"}""";
            await mutate.SaveChangesAsync();
        }

        var drifted = await content.GetAdminAsync(product);
        drifted.IsStale.Should().BeTrue("the binding mismatch is staleness even with RequiresReview false");
        drifted.Block!.RequiresReview.Should().BeFalse();

        var status = await content.ListAdminStatusAsync();
        status.Items.Single(r => r.ProductId == product).Should().Match<ContentStatusRowDto>(
            r => r.HasBlock && r.IsStale && !r.RequiresReview);
        status.Items.Single(r => r.ProductId == f.BundleProductId).HasBlock.Should().BeFalse();
    }

    [Fact]
    public async Task DraftBundlePlan_AndExtrasPricingState_ServeTheAdminTruth()
    {
        var h = new BoxTestHarness();
        await h.BuildAsync("jollof");

        // D3 — a DRAFT bundle's plan is invisible to the Active-only public read
        // but must be visible to its author by product id.
        var draftBundle = await h.Products().CreateProductAsync(new CreateProductCommand(
            "winter-box", "Winter Box", Aonik.Commerce.Entities.Catalog.ProductKinds.Bundle,
            Status: Aonik.Commerce.Entities.Catalog.ProductStatuses.Draft));
        await h.Plans().UpsertAsync(draftBundle.Id, new UpsertBundleSizePlanCommand(4, 12, 4, 60m, 12m, "GBP", []));

        (await h.Plans().GetForProductAsync(draftBundle.Id)).Should().NotBeNull("the admin read is status-agnostic");
        (await h.Plans().GetBySlugAsync("winter-box")).Should().BeNull("the public read serves Active bundles only");

        // D4 — the extras collection's admin detail marks WHICH member the public
        // rail skips as unpriceable, while keeping it in the membership.
        await h.AddExtraAsync("zobo", 3.00m);
        await h.AddExtraAsync("honey-cake", 0m);   // no GBP price row — publicly skipped

        var ctx = h.Commerce();
        var extrasCatalog = new ExtrasCatalogService(
            ctx, new TestTenantProvider(h.TenantId),
            new DictionaryTenantSettingStore(h.Settings), new NullSettingProvider(),
            new GbpTenantCurrencyProvider(), h.Pricing(),
            CommerceTestHarness.NewOptionService(ctx, h.TenantId),
            new ProductContentService(ctx, new TestTenantProvider(h.TenantId),
                CommerceTestHarness.NewSelectionService(ctx, h.TenantId),
                CommerceTestHarness.NewOptionService(ctx, h.TenantId)));
        var collections = new CollectionService(
            ctx, new TestTenantProvider(h.TenantId),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CollectionService>.Instance,
            extrasCatalog);

        var extrasCollectionId = (await ctx.Collections.AsNoTracking().FirstAsync(c => c.Slug == "extras")).Id;
        var adminDetail = await collections.GetAdminAsync(extrasCollectionId);

        var priced = adminDetail.Items.Single(i => i.Slug == "zobo");
        priced.IsPriceable.Should().BeTrue();
        priced.UnitPrice.Should().Be(3.00m);
        priced.Currency.Should().Be("GBP");

        var skipped = adminDetail.Items.Single(i => i.Slug == "honey-cake");
        skipped.IsPriceable.Should().BeFalse("an ACTIVE member the public read omits-and-counts stays in the admin membership, marked");
        skipped.UnitPrice.Should().BeNull();
    }
}
