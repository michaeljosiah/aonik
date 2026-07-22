using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 068 §6/§8/§12 — box-cart write semantics: merge, split, capacity, availability, drift
/// repair, the R10 access boundary and the R-rules, all over the launch pricing table.
/// </summary>
public class BoxCartServiceTests
{
    private static JsonElement Sel(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CartAccessContext Token(BoxCartDto dto) => CartAccessContext.ForGuest(dto.CartToken);

    private static async Task<(BoxTestHarness H, BoxTestHarness.BoxFixture F, BoxCartDto Box)> ArrangeAsync(
        int size = 6, params string[] dishes)
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync(dishes.Length == 0 ? ["jollof", "egusi"] : dishes);
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, size));
        return (h, f, box);
    }

    // ─── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Should_MintTheToken_PriceFromThePlan_AndValidateSize()
    {
        var (h, f, box) = await ArrangeAsync();

        box.CartToken.Should().NotBeNullOrEmpty("the create response is the ONLY disclosure (R10)");
        box.CartToken!.Length.Should().BeGreaterThanOrEqualTo(43, "tokens are server-minted at 256 bits");
        box.Box.Currency.Should().Be("GBP");
        box.Quote.Components.Single(c => c.Key == "boxPrice").Amount.Should().Be(95m);
        box.Quote.Total.Should().Be(box.Quote.Components.Sum(c => c.Amount), "A24");

        var tooSmall = () => h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 5));
        (await tooSmall.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R1");
    }

    [Fact]
    public async Task Create_WithInvalidFirstLine_Should_CreateNothing()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");

        var act = () => h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6,
            new AddBoxLineCommand(f.DishVariants["jollof"], 1, Sel("""{"protein":"wagyu"}"""))));

        await act.Should().ThrowAsync<OptionValidationException>("R4 — 066 owns selection validity");
        (await h.Commerce().Carts.CountAsync()).Should().Be(0, "the create is atomic with its first line");
    }

    [Fact]
    public async Task Create_WithFirstLine_Should_CarryThePersonalisation()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");

        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6,
            new AddBoxLineCommand(f.DishVariants["jollof"], 2, Sel("""{"protein":"salmon"}"""))));

        var line = box.Box.Lines.Single();
        line.Quantity.Should().Be(2);
        line.PersonalisationSummary.Should().Contain("Salmon");
        line.PersonalisationAdjustment.Should().Be(3m, "salmon is +3 over the chicken default");
        box.Quote.Components.Single(c => c.Key == "personalisation").Amount.Should().Be(6m);
    }

    // ─── R10 — the access boundary ───────────────────────────────────────────

    [Fact]
    public async Task Access_Should_FailClosedTo404_OnAbsentWrongOrLegacyTokens()
    {
        var (h, _, box) = await ArrangeAsync();
        var carts = h.BoxCarts();

        var absent = () => carts.GetAsync(box.Box.CartId, CartAccessContext.ForGuest(null));
        var wrong = () => carts.GetAsync(box.Box.CartId, CartAccessContext.ForGuest(new string('x', 43)));
        await absent.Should().ThrowAsync<NotFoundException>("no oracle — same 404 as an unknown cart");
        await wrong.Should().ThrowAsync<NotFoundException>();

        // A legacy cart whose stored token predates minting (short, client-supplied) fails closed
        // even when the caller presents that exact stored value.
        await using (var ctx = h.Commerce())
        {
            var cart = await ctx.Carts.FirstAsync(c => c.Id == box.Box.CartId);
            cart.AnonymousToken = "guessable";
            await ctx.SaveChangesAsync();
        }
        var legacy = () => carts.GetAsync(box.Box.CartId, CartAccessContext.ForGuest("guessable"));
        await legacy.Should().ThrowAsync<NotFoundException>("weak stored tokens are not access");
    }

    [Fact]
    public async Task PartyBoundBoxes_Should_AuthorizeByPrincipal_NotToken()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var party = Guid.NewGuid();
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6, BuyerPartyId: party));

        var viaParty = await h.BoxCarts().GetAsync(box.Box.CartId, CartAccessContext.ForParty(party));
        viaParty.Box.CartId.Should().Be(box.Box.CartId);

        var viaToken = () => h.BoxCarts().GetAsync(box.Box.CartId, Token(box));
        await viaToken.Should().ThrowAsync<NotFoundException>("party carts ignore the guest token by design");
    }

    [Fact]
    public async Task GenericMutationPaths_Should_RejectBoxCarts()
    {
        var (h, f, box) = await ArrangeAsync();

        var act = () => h.Carts().AddItemAsync(
            new AddCartItemCommand(box.Box.CartId, f.DishVariants["jollof"], 1m), Token(box));

        (await act.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R11");
    }

    // ─── Merge, split, capacity, availability ────────────────────────────────

    [Fact]
    public async Task Add_Should_MergeIdenticalCanonicalSelections_WhateverTheKeyOrder()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 1, Sel("""{"protein":"salmon","side":"none"}""")), access);
        var after = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 1, Sel("""{"side":"none","protein":"salmon"}""")), access);

        after.Box.Lines.Should().HaveCount(1, "R6 — canonical equality is selection equality");
        after.Box.Lines.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Add_WithDifferentSelection_Should_CreateASecondLine()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 1, null), access);
        var after = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 1, Sel("""{"protein":"salmon"}""")), access);

        after.Box.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Capacity_Should_RejectOverflow_AndShrinkBelowUnits()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 6, null), access);

        var overflow = () => carts.AddLineAsync(box.Box.CartId,
            new AddBoxLineCommand(f.DishVariants["egusi"], 1, null), access);
        (await overflow.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R3");

        var grow = await carts.ChangeSizeAsync(box.Box.CartId, 8, access);
        grow.Quote.Components.Single(c => c.Key == "boxPrice").Amount.Should().Be(125m, "formula: 95 + 2×15");

        // R2 — 7 units in an 8-box; shrinking to 6 (a valid plan size) names the exact count.
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["egusi"], 1, null), access);
        var shrinkTooFar = () => carts.ChangeSizeAsync(box.Box.CartId, 6, access);
        (await shrinkTooFar.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("remove 1");

        // R1 — below the plan minimum is a different rule.
        var belowPlan = () => carts.ChangeSizeAsync(box.Box.CartId, 5, access);
        (await belowPlan.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R1");
    }

    [Fact]
    public async Task Availability_Should_CapAggregateDemand_AcrossMerges()
    {
        var (h, f, box) = await ArrangeAsync(size: 12, "jollof", "egusi");
        await h.Inventory().SetOnHandAsync(f.DishVariants["jollof"], 3m);
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 2, null), access);

        // The merge would take cart-wide demand to 4 over an availability of 3 (R5) — repeated
        // merges cannot creep past the ceiling.
        var creep = () => carts.AddLineAsync(box.Box.CartId,
            new AddBoxLineCommand(f.DishVariants["jollof"], 2, null), access);
        (await creep.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R5");
    }

    [Fact]
    public async Task UpdateQuantity_Should_DeleteAtZero_AndRejectNegatives()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        var added = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 2, null), access);
        var lineId = added.Box.Lines.Single().LineId;

        var negative = () => carts.UpdateLineAsync(box.Box.CartId, lineId, new UpdateBoxLineCommand(Quantity: -3), access);
        (await negative.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R12");

        var afterZero = await carts.UpdateLineAsync(box.Box.CartId, lineId, new UpdateBoxLineCommand(Quantity: 0), access);
        afterZero.Box.Lines.Should().BeEmpty("quantity 0 deletes the line");
    }

    [Fact]
    public async Task SplitUpdate_Should_MoveUnitsToTheNewSelection_Atomically()
    {
        // A6/FR-10.5 — line qty 3, update 1 unit to a new selection → original 2, new line 1.
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        var added = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 3, null), access);
        var lineId = added.Box.Lines.Single().LineId;

        var after = await carts.UpdateLineAsync(box.Box.CartId, lineId,
            new UpdateBoxLineCommand(Personalisation: Sel("""{"protein":"salmon"}"""), ApplyToUnits: 1), access);

        after.Box.Lines.Should().HaveCount(2);
        after.Box.Lines.Single(l => l.LineId == lineId).Quantity.Should().Be(2);
        after.Box.Lines.Single(l => l.LineId != lineId).Quantity.Should().Be(1);
        after.Quote.UnitsSelected.Should().Be(3, "a split never changes total units");
    }

    [Fact]
    public async Task FullRepersonalisation_Should_MergeIntoAnIdenticalLine()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 1, Sel("""{"protein":"salmon"}""")), access);
        var added = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 2, null), access);
        var defaultLine = added.Box.Lines.Single(l => l.IsDefaultPersonalisation);

        var after = await carts.UpdateLineAsync(box.Box.CartId, defaultLine.LineId,
            new UpdateBoxLineCommand(Personalisation: Sel("""{"protein":"salmon"}""")), access);

        after.Box.Lines.Should().HaveCount(1);
        after.Box.Lines.Single().Quantity.Should().Be(3);
    }

    // ─── Quote (§7) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Quote_Should_SumComponents_WithSignedPersonalisation()
    {
        // A7 — the default wild-rice side costs 2.00; switching to the 0.00 side drops the quote.
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        var after = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 2, Sel("""{"side":"none"}""")), access);

        after.Box.Lines.Single().PersonalisationAdjustment.Should().Be(-2m);
        after.Quote.Components.Single(c => c.Key == "personalisation").Amount.Should().Be(-4m, "signed, ×2 units");
        after.Quote.Total.Should().Be(after.Quote.Components.Sum(c => c.Amount), "A24");
        after.Quote.SpacesLeft.Should().Be(4);
        after.Quote.IsFull.Should().BeFalse();
    }

    [Fact]
    public async Task Quote_Should_ReadDeliveryDisplayValues_FromSettings()
    {
        var h = new BoxTestHarness();
        h.Settings["Commerce.Storefront.DeliveryListAmount"] = "10.00";
        h.Settings["Commerce.Storefront.DeliveryChargedAmount"] = "2.50";
        var f = await h.BuildAsync("jollof");

        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));

        box.Quote.DeliveryList.Should().Be(10m, "struck-through display value, not a component");
        box.Quote.Components.Single(c => c.Key == "deliveryCharged").Amount.Should().Be(2.50m);
        box.Quote.Total.Should().Be(95m + 2.50m);
    }

    [Fact]
    public async Task PresetSizes_Should_OverrideTheFormula()
    {
        // A8 — 12 quotes the 170.00 preset, not the formula's 185.00.
        var (h, f, box) = await ArrangeAsync(size: 12);

        box.Quote.Components.Single(c => c.Key == "boxPrice").Amount.Should().Be(170m);
    }

    // ─── Continue gate (R8) and R9 ───────────────────────────────────────────

    [Fact]
    public async Task Continue_Should_NameTheShortfall_AndPassWhenFull()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 5, null), access);
        var gate = () => carts.ContinueAsync(box.Box.CartId, access);
        (await gate.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("add 1");

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["egusi"], 1, null), access);
        var full = await carts.ContinueAsync(box.Box.CartId, access);
        full.Quote.IsFull.Should().BeTrue();
    }

    [Fact]
    public async Task Writes_Should_Reject_AfterCheckoutStampsTheOrder()
    {
        var (h, f, box) = await ArrangeAsync();
        await using (var ctx = h.Commerce())
        {
            var cart = await ctx.Carts.FirstAsync(c => c.Id == box.Box.CartId);
            cart.OrderId = Guid.NewGuid();
            await ctx.SaveChangesAsync();
        }

        var act = () => h.BoxCarts().AddLineAsync(box.Box.CartId,
            new AddBoxLineCommand(f.DishVariants["jollof"], 1, null), Token(box));

        (await act.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("R9");
    }

    // ─── §8 drift ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetiredChoice_Should_RemapToTheDefault_ReportIt_AndMergeWhenIdentical()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        // One default line and one salmon line; retiring salmon remaps that line onto the
        // default combination — which then merges into the default line (A12 + remap-then-merge).
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 2, null), access);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 1, Sel("""{"protein":"salmon"}""")), access);

        var salmonId = await f.Options.ChoiceIdAsync("protein", "salmon");
        var options = CommerceTestHarness.NewOptionService(h.Commerce(), h.TenantId);
        await options.UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", IsActive: false));

        var after = await carts.GetAsync(box.Box.CartId, access);

        after.Changes.Should().Contain(c => c.Reason == "option-retired" && c.Group == "protein");
        after.Changes.Should().Contain(c => c.Reason == "line-merged");
        after.Box.Lines.Should().HaveCount(1, "the remapped selection equalled the default line's");
        after.Box.Lines.Single().Quantity.Should().Be(3);
    }

    [Fact]
    public async Task UnavailableLines_Should_Flag_BlockContinue_AndStay()
    {
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 3, null), access);
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["egusi"], 3, null), access);
        await h.Inventory().SetOnHandAsync(f.DishVariants["egusi"], 0m);

        var after = await carts.GetAsync(box.Box.CartId, access);
        after.Box.Lines.Should().HaveCount(2, "A13 — unavailable lines never vanish silently");
        after.Box.Lines.Single(l => l.VariantId == f.DishVariants["egusi"]).IsUnavailable.Should().BeTrue();
        after.Changes.Should().Contain(c => c.Reason == "unavailable");

        var gate = () => carts.ContinueAsync(box.Box.CartId, access);
        (await gate.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("unavailable");

        var increase = () => carts.UpdateLineAsync(box.Box.CartId,
            after.Box.Lines.Single(l => l.IsUnavailable).LineId, new UpdateBoxLineCommand(Quantity: 4), access);
        await increase.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task QuantityDecrease_Should_ClearAStaleUnavailableFlag()
    {
        // J5 — flags are authoritative for the state the write RETURNS: reducing an
        // over-demanded line back under availability must not keep reporting it unavailable.
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        var added = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 6, null), access);
        await h.Inventory().SetOnHandAsync(f.DishVariants["jollof"], 5m);

        var flagged = await carts.GetAsync(box.Box.CartId, access);
        flagged.Box.Lines.Single().IsUnavailable.Should().BeTrue("demand 6 exceeds availability 5");

        var after = await carts.UpdateLineAsync(box.Box.CartId, added.Box.Lines.Single().LineId,
            new UpdateBoxLineCommand(Quantity: 4), access);

        after.Box.Lines.Single().IsUnavailable.Should().BeFalse("demand 4 fits availability 5");
        after.Changes.Should().NotContain(c => c.Reason == "unavailable");
    }

    [Fact]
    public async Task DeletingALine_Should_NotCountItsDemand_AgainstTheSurvivors()
    {
        // K1 — a deleted line's quantity must leave cart-wide demand before the authoritative
        // availability pass, or the surviving identical-variant line gets wrongly flagged.
        var (h, f, box) = await ArrangeAsync();
        var carts = h.BoxCarts();
        var access = Token(box);

        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 3, null), access);
        var two = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 3, Sel("""{"protein":"salmon"}""")), access);
        await h.Inventory().SetOnHandAsync(f.DishVariants["jollof"], 4m);

        var salmonLine = two.Box.Lines.Single(l => !l.IsDefaultPersonalisation);
        var after = await carts.RemoveLineAsync(box.Box.CartId, salmonLine.LineId, access);

        after.Box.Lines.Single().IsUnavailable.Should().BeFalse("3 remaining units fit availability 4");
        after.Changes.Should().NotContain(c => c.Reason == "unavailable");
    }

    // ─── A6 — abandonment ────────────────────────────────────────────────────

    [Fact]
    public async Task IdleBoxSessions_Should_Abandon_AfterTheWindow()
    {
        var (h, _, box) = await ArrangeAsync();

        var abandoned = await h.Maintenance().AbandonIdleBoxCartsAsync(DateTime.UtcNow.AddDays(15));

        abandoned.Should().Be(1);
        await using var ctx = h.Commerce();
        (await ctx.Carts.FirstAsync(c => c.Id == box.Box.CartId)).Status.Should().Be(CartStatuses.Abandoned);
    }
}
