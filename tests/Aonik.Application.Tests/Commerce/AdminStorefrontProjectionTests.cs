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
        var ctx = h.Commerce();
        return new AdminStorefrontService(
            ctx, tenant, spine,
            new StorefrontOrderService(h.Commerce(), tenant, spine),
            CommerceTestHarness.NewOptionService(ctx, h.TenantId),
            CommerceTestHarness.NewSelectionService(ctx, h.TenantId),
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
        row.PaymentStatus.Should().NotBe(CheckoutPaymentStatuses.Captured, "nothing has completed yet");
        row.FulfilmentStatus.Should().Be("Unfulfilled");
        row.Total.Should().Be(95m + 9m, "the box goods total plus two £4.50 add-ons");

        // A PENDING-PAYMENT cart (Open but claimed by an order) is frozen, not a
        // live session: its charge is fixed and its snapshots are the recorded
        // truth — a retail price moving AFTER the claim must not flag it (the
        // customer is mid-payment against the recorded amount).
        await using (var reprice = h.Commerce())
        {
            var price = await reprice.ProductPrices.FirstAsync(p => p.ProductVariantId == extraVariant);
            price.Amount += 2m;
            await reprice.SaveChangesAsync();
        }
        var pendingRow = (await admin.ListCartsAsync()).Items.Single(r => r.OrderId == checkout.OrderId);
        pendingRow.Total.Should().Be(95m + 9m, "a claimed cart serves the recorded charge total");
        pendingRow.BoxMeta!.Drift.Should().BeFalse("a claimed cart is not revalidated against live state");
        (await admin.GetCartAsync(pendingRow.CartId))!.Lines
            .Should().OnlyContain(l => !l.IsUnavailable && !l.PriceChanged && l.SelectionDrift.Count == 0);

        // Payment completion must converge the DURABLE funding record the
        // projection reads — the at-creation provider status is not the truth
        // once PaymentCompletedEvent fires. Fulfilment does NOT flip: payment is
        // not delivery evidence, so the paid order stays awaiting fulfilment
        // until a real fulfilment lifecycle records the fact.
        await h.Checkout().ConfirmPaymentAsync(checkout.OrderId);
        var confirmed = (await admin.ListOrdersAsync()).Items.Single();
        confirmed.PaymentStatus.Should().Be(CheckoutPaymentStatuses.Captured);
        confirmed.FulfilmentStatus.Should().Be("Unfulfilled");
        (await admin.ListOrdersAsync(paymentStatus: CheckoutPaymentStatuses.Captured))
            .TotalCount.Should().Be(1, "the payment-status filter matches the converged value");

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
        detail.PaymentStatus.Should().Be(confirmed.PaymentStatus, "detail and list read the same durable record");
        detail.FulfilmentStatus.Should().Be("Unfulfilled");
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
        row.BoxMeta.Drift.Should().BeFalse();
        row.ItemCount.Should().Be(5m);
        row.Total.Should().Be(95m + 3.50m,
            "an open box cart's value is the box CONTAINER price plus add-ons — dish snapshots are deliberately zero");

        var before = await admin.GetCartAsync(box.Box.CartId);
        before!.Lines.Should().OnlyContain(l => !l.IsUnavailable && !l.PriceChanged);

        // Three distinct legs of the availability/price predicate, none persisted:
        // the dish's stock ROWS are deleted outright (a missing level row means
        // ZERO, not unknown), the zobo variant is deactivated (catalogue state,
        // stock untouched), and chin-chin's retail price is bumped (A18 drift).
        var (_, chinChinVariant) = await h.AddExtraAsync("chin-chin", 2.00m);
        await carts.AddExtraLineAsync(box.Box.CartId, new AddBoxExtraCommand(chinChinVariant, 1), access);
        await using (var ctx = h.Commerce())
        {
            var levels = await ctx.InventoryLevels
                .Where(l => l.ProductVariantId == f.DishVariants["jollof"]).ToListAsync();
            ctx.InventoryLevels.RemoveRange(levels);
            var zobo = await ctx.ProductVariants.SingleAsync(v => v.Id == extraVariant);
            zobo.IsActive = false;
            var price = await ctx.ProductPrices.FirstAsync(p => p.ProductVariantId == chinChinVariant);
            price.Amount += 1m;
            await ctx.SaveChangesAsync();
        }

        var after = await admin.GetCartAsync(box.Box.CartId);
        after!.Lines.Single(l => l.Kind == "BoxDish").IsUnavailable.Should().BeTrue(
            "no stock row at all must read as zero available, exactly like the box path");
        after.Lines.Single(l => l.Sku.Contains("zobo")).IsUnavailable.Should().BeTrue(
            "a deactivated variant is unavailable regardless of stock");
        var chinChin = after.Lines.Single(l => l.Sku.Contains("chin-chin"));
        chinChin.PriceChanged.Should().BeTrue();
        chinChin.IsUnavailable.Should().BeFalse();

        // Spec 083's list contract: the row itself carries the checkout-blocked
        // signal — the carts table must not need a detail call per row.
        var driftedRow = (await admin.ListCartsAsync()).Items.Single(r => r.CartId == box.Box.CartId);
        driftedRow.BoxMeta!.Drift.Should().BeTrue();

        // Read-only proof: the stored add-on snapshot is untouched — the flag was
        // computed against the live price, not written back (repair stays the
        // customer load path's job).
        await using var verify = h.Commerce();
        (await verify.CartItems.SingleAsync(i => i.CartId == box.Box.CartId && i.Sku.Contains("chin-chin")))
            .UnitPriceSnapshot.Should().Be(2.00m);
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
        var options = CommerceTestHarness.NewOptionService(ctx, h.TenantId);
        var content = new ProductContentService(
            ctx, new TestTenantProvider(h.TenantId),
            CommerceTestHarness.NewSelectionService(ctx, h.TenantId),
            options);

        // A single-select AND a multi-select group, so the stored binding has both
        // canonical value shapes (bare string + array) — the list read's batched
        // canonicalisation must be byte-identical to the write path's.
        var sauce = await options.CreateGroupAsync(new CreateOptionGroupCommand("sauce", "Sauce"));
        await options.AddChoiceAsync(sauce.Id, new AddOptionChoiceCommand("mild", "Mild", IsRecommendedDefault: true));
        var sides = await options.CreateGroupAsync(new CreateOptionGroupCommand(
            "sides", "Sides", SelectionMode: Aonik.Commerce.Entities.Catalog.OptionSelectionModes.Multi));
        await options.AddChoiceAsync(sides.Id, new AddOptionChoiceCommand("plantain", "Plantain", IsRecommendedDefault: true));
        await options.AddChoiceAsync(sides.Id, new AddOptionChoiceCommand("salad", "Salad"));
        await options.SetProductOptionGroupsAsync(product, new SetProductOptionGroupsCommand(
        [
            new ProductOptionGroupLine("sauce", SortOrder: 0),
            new ProductOptionGroupLine("sides", SortOrder: 1),
        ]));

        await content.UpsertContentAsync(product, new UpsertProductContentCommand("Per serving", Kcal: 400));

        var fresh = await content.GetAdminAsync(product);
        fresh.Block.Should().NotBeNull();
        fresh.IsStale.Should().BeFalse();
        (await content.ListAdminStatusAsync()).Items.Single(r => r.ProductId == product).IsStale
            .Should().BeFalse("the batched all-defaults canonical form must match the write path byte-for-byte");

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

        // An ACTIVE member that is structurally ineligible for the rail (a Bundle,
        // not a Simple product): also absent from the rail, but no price row can
        // fix it — IsPriceable must stay null, never claim a pricing problem.
        var bundleMember = await h.Products().CreateProductAsync(new CreateProductCommand(
            "gift-hamper", "Gift Hamper", Aonik.Commerce.Entities.Catalog.ProductKinds.Bundle));
        await using (var seed = h.Commerce())
        {
            var extras = await seed.Collections.FirstAsync(c => c.Slug == "extras");
            seed.CollectionItems.Add(new Aonik.Commerce.Entities.Catalog.CollectionItem
            {
                Id = Guid.NewGuid(), TenantId = h.TenantId, CollectionId = extras.Id,
                ProductId = bundleMember.Id, Rank = 99,
            });
            await seed.SaveChangesAsync();
        }

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
            extrasCatalog, new GbpTenantCurrencyProvider(), h.Pricing());

        var extrasCollectionId = (await ctx.Collections.AsNoTracking().FirstAsync(c => c.Slug == "extras")).Id;
        var adminDetail = await collections.GetAdminAsync(extrasCollectionId);

        var priced = adminDetail.Items.Single(i => i.Slug == "zobo");
        priced.IsPriceable.Should().BeTrue();
        priced.UnitPrice.Should().Be(3.00m);
        priced.Currency.Should().Be("GBP");

        var skipped = adminDetail.Items.Single(i => i.Slug == "honey-cake");
        skipped.IsPriceable.Should().BeFalse("an ACTIVE member the public read omits-and-counts stays in the admin membership, marked");
        skipped.UnitPrice.Should().BeNull();

        var ineligible = adminDetail.Items.Single(i => i.Slug == "gift-hamper");
        ineligible.IsPriceable.Should().BeNull(
            "false is a PRICING verdict — a structurally ineligible member must not be told to repair pricing");

        // A PARKED (inactive) extras collection serves no public rail at all —
        // pricing state is meaningless, so no member may be told to repair
        // pricing that is perfectly valid.
        await using (var park = h.Commerce())
        {
            var extras = await park.Collections.FirstAsync(c => c.Id == extrasCollectionId);
            extras.IsActive = false;
            await park.SaveChangesAsync();
        }
        var parked = await collections.GetAdminAsync(extrasCollectionId);
        parked.Items.Should().OnlyContain(i => i.IsPriceable == null && i.UnitPrice == null);
    }

    [Fact]
    public async Task CartsAdmin_SurfacesPersonalisationDrift_ThroughTheSpec066Rules()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var product = f.DishProducts["jollof"];
        var ctx = h.Commerce();
        var options = CommerceTestHarness.NewOptionService(ctx, h.TenantId);

        // sauce: mild (default, £0) / hot (+£1 absolute) — the customer picks hot.
        var sauce = await options.CreateGroupAsync(new CreateOptionGroupCommand("sauce", "Sauce"));
        await options.AddChoiceAsync(sauce.Id, new AddOptionChoiceCommand("mild", "Mild", IsRecommendedDefault: true));
        var hot = await options.AddChoiceAsync(sauce.Id, new AddOptionChoiceCommand("hot", "Hot", Price: 1.00m));
        await options.SetProductOptionGroupsAsync(product, new SetProductOptionGroupsCommand(
            [new ProductOptionGroupLine("sauce", SortOrder: 0)]));

        var carts = h.BoxCarts();
        var box = await carts.CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        var access = CartAccessContext.ForGuest(box.CartToken);
        using var selection = System.Text.Json.JsonDocument.Parse("""{"sauce":"hot"}""");
        await carts.AddLineAsync(box.Box.CartId,
            new AddBoxLineCommand(f.DishVariants["jollof"], 2, selection.RootElement), access);

        var admin = AdminSvc(h);
        (await admin.GetCartAsync(box.Box.CartId))!.Lines.Single()
            .Should().Match<AdminCartLineDto>(l => !l.PriceChanged && l.SelectionDrift.Count == 0);

        // Retire the chosen option: renormalisation remaps hot → mild, which
        // both REPORTS the drift and reprices the line (stored +£1 vs £0 now) —
        // exactly what the customer's next load will do, computed read-only here.
        await using (var retire = h.Commerce())
        {
            var choice = await retire.OptionChoices.FirstAsync(c => c.Id == hot.Id);
            choice.IsActive = false;
            await retire.SaveChangesAsync();
        }

        var line = (await admin.GetCartAsync(box.Box.CartId))!.Lines.Single();
        line.PriceChanged.Should().BeTrue("the stored +£1 adjustment no longer matches the renormalised £0");
        line.SelectionDrift.Should().ContainSingle()
            .Which.Should().Match<Aonik.Commerce.Contracts.Models.Catalog.SelectionDrift>(d =>
                d.GroupKey == "sauce" && d.FromChoiceKey == "hot" && d.Reason == "option-retired");

        var row = (await admin.ListCartsAsync()).Items.Single(r => r.CartId == box.Box.CartId);
        row.BoxMeta!.Drift.Should().BeTrue("the list row must carry the checkout-blocked signal");

        // Read-only proof: the stored selection and adjustment are untouched.
        await using var verify = h.Commerce();
        var storedLine = await verify.CartItems.SingleAsync(i => i.CartId == box.Box.CartId);
        storedLine.PersonalisationAdjustment.Should().Be(1.00m);
        storedLine.PersonalisationJson.Should().Contain("hot");
    }

    [Fact]
    public async Task CartsAdmin_ResolvesBundleLines_ThroughTheirComponentSelections()
    {
        var h = new BoxTestHarness();
        await h.BuildAsync("jollof");
        var party = Guid.NewGuid();

        // A classic Spec 042 bundle: the cart line's own id is the bundle
        // PRODUCT; the inventory-bearing variant lives in the selections.
        var (_, cakeVariant) = await h.AddExtraAsync("cake", 6.00m, inExtrasCollection: false, stock: 5m);
        var hamper = await h.Products().CreateProductAsync(new CreateProductCommand(
            "hamper", "Hamper", Aonik.Commerce.Entities.Catalog.ProductKinds.Bundle,
            BundlePricingMode: "SumOfComponents"));
        var slot = await h.Products().AddBundleSlotAsync(new AddBundleSlotCommand(hamper.Id, "Pick 1", 1, 1));

        var cart = await h.Carts().CreateCartAsync(new CreateCartCommand("GBP", BuyerPartyId: party));
        var access = CartAccessContext.ForParty(party);
        await h.Carts().AddBundleAsync(new AddBundleToCartCommand(
            cart.Id, hamper.Id, [new BundleSelectionLine(slot.Id, cakeVariant)]), access);

        var admin = AdminSvc(h);
        var line = (await admin.GetCartAsync(cart.Id))!.Lines.Single();
        line.IsUnavailable.Should().BeFalse(
            "a bundle line's availability resolves through its COMPONENT variants, never by treating the bundle id as a variant");
        line.Components.Should().ContainSingle().Which.Should()
            .Match<AdminCartLineComponentDto>(c => c.ProductVariantId == cakeVariant && !c.IsUnavailable);

        // Kill the component's stock: the component flags and the line follows.
        await h.Inventory().SetOnHandAsync(cakeVariant, 0m);
        var starved = (await admin.GetCartAsync(cart.Id))!.Lines.Single();
        starved.Components.Single().IsUnavailable.Should().BeTrue();
        starved.IsUnavailable.Should().BeTrue();

        // A REMOVAL is activity too: the soft-deleted row's write must advance
        // the cart's reported timestamp even though the parent row never moves.
        var before = (await admin.GetCartAsync(cart.Id))!.UpdatedAtUtc;
        var removedLineId = (await h.Commerce().CartItems.AsNoTracking()
            .SingleAsync(i => i.CartId == cart.Id)).Id;
        await h.Carts().RemoveItemAsync(cart.Id, removedLineId, access);
        (await admin.GetCartAsync(cart.Id))!.UpdatedAtUtc.Should().BeOnOrAfter(before);
    }
}
