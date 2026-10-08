using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

public class BoxCartAdoptionTests
{
    [Fact]
    public async Task AdoptAsync_Should_RequireAnExplicitChoice_WithoutChangingEitherBox()
    {
        var fixture = await CreatePairAsync();
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId,
            fixture.PartyId, CartAccessContext.ForGuest(fixture.Guest.CartToken));

        var conflict = (await act.Should().ThrowAsync<ActiveBoxConflictException>()).Which;
        conflict.Code.Should().Be(ActiveBoxConflictException.ChoiceRequired);
        conflict.Guest!.CartId.Should().Be(fixture.Guest.Box.CartId);
        conflict.SavedCandidates.Should().ContainSingle().Which.CartId.Should().Be(fixture.Saved.Box.CartId);
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Theory]
    [InlineData(CartAdoptionDecisions.KeepGuest)]
    [InlineData(CartAdoptionDecisions.UseSaved)]
    public async Task AdoptAsync_Should_PreserveBothBoxesContents_AndRetireGuestAccess_WhenChoosingOne(string decision)
    {
        var fixture = await CreatePairAsync();
        var before = await SnapshotAsync(fixture.Harness);

        var result = await AdoptAsync(fixture, Choice(fixture, decision));

        var keptId = decision == CartAdoptionDecisions.KeepGuest ? fixture.Guest.Box.CartId : fixture.Saved.Box.CartId;
        var discardedId = decision == CartAdoptionDecisions.KeepGuest ? fixture.Saved.Box.CartId : fixture.Guest.Box.CartId;
        result.Id.Should().Be(keptId);
        result.BuyerPartyId.Should().Be(fixture.PartyId);
        result.AnonymousToken.Should().BeNull();
        var after = await SnapshotAsync(fixture.Harness);
        after.Single(x => x.Id == keptId).Status.Should().Be(CartStatuses.Open);
        after.Single(x => x.Id == discardedId).Status.Should().Be(CartStatuses.Abandoned);
        after.Single(x => x.Id == fixture.Guest.Box.CartId).AnonymousToken.Should().BeNull();
        after.Should().OnlyContain(x => x.BuyerPartyId == fixture.PartyId && x.OrderId == null);
        after.SelectMany(x => x.Items).Should().BeEquivalentTo(before.SelectMany(x => x.Items), options =>
            options.ComparingByMembers<CartItem>().ComparingByMembers<CartItemSelection>());
        var tokenRead = await fixture.Harness.Carts().GetCartAsync(fixture.Guest.Box.CartId,
            CartAccessContext.ForGuest(fixture.Guest.CartToken));
        tokenRead.Should().BeNull();
    }

    [Theory]
    [InlineData(CartAdoptionDecisions.KeepGuest)]
    [InlineData(CartAdoptionDecisions.UseSaved)]
    public async Task AdoptAsync_Should_ReplayTheChosenOutcome_WithoutAnotherWrite(string decision)
    {
        var fixture = await CreatePairAsync();
        var choice = Choice(fixture, decision);
        var first = await AdoptAsync(fixture, choice);
        var beforeReplay = await SnapshotAsync(fixture.Harness);

        var second = await fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId,
            fixture.PartyId, CartAccessContext.ForParty(fixture.PartyId), choice);

        second.Id.Should().Be(first.Id);
        await AssertUnchangedAsync(fixture.Harness, beforeReplay);
    }

    [Theory]
    [InlineData(CartAdoptionDecisions.KeepGuest, CartAdoptionDecisions.UseSaved)]
    [InlineData(CartAdoptionDecisions.UseSaved, CartAdoptionDecisions.KeepGuest)]
    public async Task AdoptAsync_Should_NotApplyTheOppositeDecision_AfterAChoiceAlreadyCommitted(string first, string second)
    {
        var fixture = await CreatePairAsync();
        await AdoptAsync(fixture, Choice(fixture, first));
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId, fixture.PartyId,
            CartAccessContext.ForParty(fixture.PartyId), Choice(fixture, second));

        await act.Should().ThrowAsync<ActiveBoxConflictException>();
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdoptAsync_Should_RejectAStaleVersionOfEitherBox_WithoutDiscardingAnything(bool guestChanged)
    {
        var fixture = await CreatePairAsync();
        var choice = Choice(fixture, CartAdoptionDecisions.KeepGuest);
        await using (var context = fixture.Harness.Commerce())
        {
            var cart = await context.Carts.SingleAsync(x => x.Id ==
                (guestChanged ? fixture.Guest.Box.CartId : fixture.Saved.Box.CartId));
            cart.RowVersion = [1, 2, 3, 4];
            await context.SaveChangesAsync();
        }
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => AdoptAsync(fixture, choice);

        (await act.Should().ThrowAsync<ActiveBoxConflictException>()).Which.Code
            .Should().Be(ActiveBoxConflictException.StaleChoice);
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "not-base64")]
    [InlineData(false, "not-base64")]
    public async Task AdoptAsync_Should_RejectMissingOrMalformedVersions(bool guestVersion, string? version)
    {
        var fixture = await CreatePairAsync();
        var choice = Choice(fixture, CartAdoptionDecisions.KeepGuest);
        choice = guestVersion ? choice with { ExpectedGuestCartVersion = version! }
            : choice with { ExpectedSavedCartVersion = version! };
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => AdoptAsync(fixture, choice);

        await act.Should().ThrowAsync<ActiveBoxConflictException>();
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Fact]
    public async Task AdoptAsync_Should_NotRevealSavedCandidates_WithoutTheGuestToken()
    {
        var fixture = await CreatePairAsync();
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId, fixture.PartyId,
            CartAccessContext.ForGuest("incorrect-token"), Choice(fixture, CartAdoptionDecisions.UseSaved));

        await act.Should().ThrowAsync<NotFoundException>();
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdoptAsync_Should_NotAdoptOrDiscardABoxWithAPendingOrder(bool guestHasOrder)
    {
        var fixture = await CreatePairAsync();
        await using (var context = fixture.Harness.Commerce())
        {
            var cart = await context.Carts.SingleAsync(x => x.Id ==
                (guestHasOrder ? fixture.Guest.Box.CartId : fixture.Saved.Box.CartId));
            cart.OrderId = Guid.NewGuid();
            await context.SaveChangesAsync();
        }
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => AdoptAsync(fixture, Choice(fixture, CartAdoptionDecisions.KeepGuest));

        if (guestHasOrder) await act.Should().ThrowAsync<StorefrontValidationException>();
        else await act.Should().ThrowAsync<ActiveBoxConflictException>();
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Fact]
    public async Task AdoptAsync_Should_NotResolveLegacyMultipleBoxes_UsingAChoiceForOnlyOnePair()
    {
        var fixture = await CreatePairAsync();
        var another = await fixture.Harness.BoxCarts().CreateAsync(
            new CreateBoxCartCommand(fixture.Saved.Box.BundleProductId, 6));
        await using (var context = fixture.Harness.Commerce())
        {
            var cart = await context.Carts.SingleAsync(x => x.Id == another.Box.CartId);
            cart.BuyerPartyId = fixture.PartyId;
            cart.AnonymousToken = null;
            await context.SaveChangesAsync();
        }
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => AdoptAsync(fixture, Choice(fixture, CartAdoptionDecisions.KeepGuest));

        var conflict = (await act.Should().ThrowAsync<ActiveBoxConflictException>()).Which;
        conflict.Code.Should().Be(ActiveBoxConflictException.Multiple);
        conflict.SavedCandidates.Select(x => x.CartId).Should().BeEquivalentTo([fixture.Saved.Box.CartId, another.Box.CartId]);
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Fact]
    public async Task UseSavedReplay_Should_ReadTheExactOwnedSavedBox_WithItsLatestContents()
    {
        var fixture = await CreatePairAsync();
        var choice = Choice(fixture, CartAdoptionDecisions.UseSaved);
        await AdoptAsync(fixture, choice);
        await using (var context = fixture.Harness.Commerce())
        {
            var saved = await context.Carts.Include(x => x.Items).SingleAsync(x => x.Id == fixture.Saved.Box.CartId);
            saved.Items.Single().Quantity = 3;
            saved.RowVersion = [9, 8, 7];
            await context.SaveChangesAsync();
        }
        var before = await SnapshotAsync(fixture.Harness);

        var replay = await fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId,
            fixture.PartyId, CartAccessContext.ForParty(fixture.PartyId), choice);

        replay.Id.Should().Be(fixture.Saved.Box.CartId);
        replay.Items.Single().Quantity.Should().Be(3);
        replay.CartVersion.Should().Be(Convert.ToBase64String([9, 8, 7]));
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Fact]
    public async Task UseSavedReplay_Should_NotFallBackToANewBox_WhenItsOriginalTargetHasCheckedOut()
    {
        var fixture = await CreatePairAsync();
        var choice = Choice(fixture, CartAdoptionDecisions.UseSaved);
        await AdoptAsync(fixture, choice);
        await using (var context = fixture.Harness.Commerce())
        {
            var saved = await context.Carts.SingleAsync(x => x.Id == fixture.Saved.Box.CartId);
            saved.OrderId = Guid.NewGuid();
            await context.SaveChangesAsync();
        }
        var next = await fixture.Harness.BoxCarts().CreateAsync(
            new CreateBoxCartCommand(fixture.Saved.Box.BundleProductId, 6, BuyerPartyId: fixture.PartyId));
        var before = await SnapshotAsync(fixture.Harness);

        var act = () => fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId,
            fixture.PartyId, CartAccessContext.ForParty(fixture.PartyId), choice);

        await act.Should().ThrowAsync<ActiveBoxConflictException>();
        next.Box.CartId.Should().NotBe(choice.ExpectedSavedCartId);
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Fact]
    public async Task AdoptAsync_Should_RejectAChangedTargetIdentity_InsteadOfChoosingTheCurrentBox()
    {
        var fixture = await CreatePairAsync();
        var before = await SnapshotAsync(fixture.Harness);
        var choice = Choice(fixture, CartAdoptionDecisions.UseSaved) with { ExpectedSavedCartId = Guid.NewGuid() };

        var act = () => AdoptAsync(fixture, choice);

        (await act.Should().ThrowAsync<ActiveBoxConflictException>()).Which.Code.Should().Be(ActiveBoxConflictException.StaleChoice);
        await AssertUnchangedAsync(fixture.Harness, before);
    }

    [Fact]
    public async Task AdoptAsync_Should_PreserveTheOldCancellationOverload_ForGenericCarts()
    {
        var harness = new BoxTestHarness();
        var generic = await harness.Carts().CreateCartAsync(new CreateCartCommand("GBP"));
        var party = Guid.NewGuid();

        var adopted = await harness.Carts().AdoptAsync(generic.Id, party,
            CartAccessContext.ForGuest(generic.AnonymousToken), CancellationToken.None);

        adopted.Id.Should().Be(generic.Id);
        adopted.BuyerPartyId.Should().Be(party);
        adopted.BoxBundleProductId.Should().BeNull();
    }

    private static async Task<Fixture> CreatePairAsync()
    {
        var harness = new BoxTestHarness();
        var catalog = await harness.BuildAsync("saved-dish", "guest-dish");
        var party = Guid.NewGuid();
        var saved = await harness.BoxCarts().CreateAsync(new CreateBoxCartCommand(catalog.BundleProductId, 6,
            new AddBoxLineCommand(catalog.DishVariants["saved-dish"], 1), party));
        var guest = await harness.BoxCarts().CreateAsync(new CreateBoxCartCommand(catalog.BundleProductId, 6,
            new AddBoxLineCommand(catalog.DishVariants["guest-dish"], 2)));
        return new(harness, party, guest, saved);
    }

    private static AdoptCartChoice Choice(Fixture fixture, string decision) => new(decision,
        fixture.Saved.Box.CartId, fixture.Saved.CartVersion, fixture.Guest.CartVersion);

    private static Task<CartDto> AdoptAsync(Fixture fixture, AdoptCartChoice choice) =>
        fixture.Harness.Carts().AdoptAsync(fixture.Guest.Box.CartId, fixture.PartyId,
            CartAccessContext.ForGuest(fixture.Guest.CartToken), choice);

    private static async Task<List<Cart>> SnapshotAsync(BoxTestHarness harness)
    {
        await using var context = harness.Commerce();
        return await context.Carts.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Selections).ToListAsync();
    }

    private static async Task AssertUnchangedAsync(BoxTestHarness harness, List<Cart> before)
    {
        (await SnapshotAsync(harness)).Should().BeEquivalentTo(before, options =>
            options.ComparingByMembers<Cart>().ComparingByMembers<CartItem>().ComparingByMembers<CartItemSelection>());
    }

    private sealed record Fixture(BoxTestHarness Harness, Guid PartyId, BoxCartDto Guest, BoxCartDto Saved);
}
