using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public partial class BoxCartServiceTests
{
    [Fact]
    public async Task GetCurrent_Should_ReturnAnEmptyCommittedBox_WithoutTokenOrActivityWrite()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        var created = await h.BoxCarts().CreateAsync(new(f.BundleProductId, 6, BuyerPartyId: partyId));
        await using var context = h.Commerce();
        var before = await context.Carts.AsNoTracking().SingleAsync();

        var current = await h.BoxCarts().GetCurrentAsync(partyId);

        current.Should().NotBeNull();
        current!.Box.CartId.Should().Be(created.Box.CartId);
        current.Box.Lines.Should().BeEmpty();
        current.Quote.SpacesLeft.Should().Be(6);
        current.CartToken.Should().BeNull();
        current.CartVersion.Should().Be(Convert.ToBase64String(before.RowVersion));
        var after = await context.Carts.AsNoTracking().SingleAsync();
        after.UpdatedAt.Should().Be(before.UpdatedAt);
        after.AnonymousToken.Should().Be(created.CartToken, "born-bound carts retain the existing adoption marker convention");
    }

    [Fact]
    public async Task GetCurrent_Should_NotCreateOrReturnGuestForeignGenericClosedOrPendingCarts()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        await using var context = h.Commerce();
        var guest = CurrentTestCart(h, f, null);
        var foreignParty = CurrentTestCart(h, f, Guid.NewGuid());
        var foreignTenant = CurrentTestCart(h, f, partyId);
        foreignTenant.TenantId = Guid.NewGuid();
        var generic = CurrentTestCart(h, f, partyId);
        generic.BoxBundleProductId = null;
        var pending = CurrentTestCart(h, f, partyId);
        pending.OrderId = Guid.NewGuid();
        var abandoned = CurrentTestCart(h, f, partyId);
        abandoned.Status = CartStatuses.Abandoned;
        var checkedOut = CurrentTestCart(h, f, partyId);
        checkedOut.Status = CartStatuses.CheckedOut;
        var deleted = CurrentTestCart(h, f, partyId);
        deleted.IsDeleted = true;
        context.Carts.AddRange(guest, foreignParty, foreignTenant, generic, pending, abandoned, checkedOut, deleted);
        await context.SaveChangesAsync();

        var current = await h.BoxCarts().GetCurrentAsync(partyId);

        current.Should().BeNull();
        (await context.Carts.IncludeSoftDeleted().AcrossTenants().CountAsync()).Should().Be(8);
    }

    [Fact]
    public async Task GetCurrent_Should_KeepTheBoxActive_WhenItsLastDishIsRemoved()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        var box = await h.BoxCarts().CreateAsync(new(f.BundleProductId, 6,
            new(f.DishVariants["jollof"], 1), partyId));

        await h.BoxCarts().RemoveLineAsync(box.Box.CartId, box.Box.Lines.Single().LineId,
            CartAccessContext.ForParty(partyId, ""));
        var current = await h.BoxCarts().GetCurrentAsync(partyId);

        current.Should().NotBeNull();
        current!.Box.CartId.Should().Be(box.Box.CartId);
        current.Box.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrent_Should_ReuseCatalogueDriftRepair_AndCurrentQuote()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        var box = await h.BoxCarts().CreateAsync(new(f.BundleProductId, 6,
            new(f.DishVariants["jollof"], 1, Sel("""{"protein":"salmon"}""")), partyId));
        var salmonId = await f.Options.ChoiceIdAsync("protein", "salmon");
        await CommerceTestHarness.NewOptionService(h.Commerce(), h.TenantId)
            .UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", IsActive: false));

        var current = await h.BoxCarts().GetCurrentAsync(partyId);

        current.Should().NotBeNull();
        current!.Changes.Should().Contain(change => change.Reason == "option-retired");
        current.Box.Lines.Single().PersonalisationSummary.Should().NotContain("Salmon");
        current.Quote.Total.Should().BeLessThan(box.Quote.Total);
        current.CartToken.Should().BeNull();
        await using var context = h.Commerce();
        var stored = await context.Carts.AsNoTracking().SingleAsync();
        current.CartVersion.Should().Be(Convert.ToBase64String(stored.RowVersion));
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnNull_WhenCheckoutFreezesTheBoxDuringQuoteConstruction()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        var box = await h.BoxCarts().CreateAsync(new(f.BundleProductId, 6, BuyerPartyId: partyId));
        var currencies = new Mock<ITenantCurrencyProvider>();
        currencies.Setup(provider => provider.GetTenantDefaultCurrencyAsync(h.TenantId, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await using var checkoutContext = h.Commerce();
                var cart = await checkoutContext.Carts.SingleAsync(row => row.Id == box.Box.CartId);
                cart.OrderId = Guid.NewGuid();
                await checkoutContext.SaveChangesAsync();
                return "GBP";
            });
        await using var context = h.Commerce();
        var service = new BoxCartService(context, new TestTenantProvider(h.TenantId),
            CommerceTestHarness.NewSelectionService(context, h.TenantId), h.Inventory(),
            new DictionaryTenantSettingStore(h.Settings), new NullSettingProvider(), currencies.Object, h.Pricing(), h.Clock,
            h.DiscountQuotes(context));

        var current = await service.GetCurrentAsync(partyId);

        current.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrent_AndCreate_Should_ReportBoundedLegacyCandidates_WithoutChangingAnyBox()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        await using var context = h.Commerce();
        var rows = Enumerable.Range(0, ActiveBoxCarts.CandidateLimit + 1)
            .Select(_ => CurrentTestCart(h, f, partyId)).ToList();
        context.Carts.AddRange(rows);
        await context.SaveChangesAsync();
        var original = await context.Carts.AsNoTracking().OrderBy(cart => cart.Id).ToListAsync();

        var read = () => h.BoxCarts().GetCurrentAsync(partyId);
        var create = () => h.BoxCarts().CreateAsync(new(f.BundleProductId, 6, BuyerPartyId: partyId));
        var readConflict = (await read.Should().ThrowAsync<ActiveBoxConflictException>()).Which;
        var createConflict = (await create.Should().ThrowAsync<ActiveBoxConflictException>()).Which;

        foreach (var conflict in new[] { readConflict, createConflict })
        {
            conflict.Code.Should().Be(ActiveBoxConflictException.Multiple);
            conflict.Guest.Should().BeNull();
            conflict.HasMore.Should().BeTrue();
            conflict.SavedCandidates.Select(candidate => candidate.CartId)
                .Should().Equal(original.Take(ActiveBoxCarts.CandidateLimit).Select(cart => cart.Id));
        }
        (await context.Carts.AsNoTracking().OrderBy(cart => cart.Id).ToListAsync())
            .Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task Create_Should_RejectAnExistingPartyBox_WithoutAddingTheRequestedFirstLine()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        var box = await h.BoxCarts().CreateAsync(new(f.BundleProductId, 6,
            new(f.DishVariants["jollof"], 1), partyId));

        var create = () => h.BoxCarts().CreateAsync(new(f.BundleProductId, 12,
            new(f.DishVariants["jollof"], 2), partyId));
        var conflict = (await create.Should().ThrowAsync<ActiveBoxConflictException>()).Which;

        conflict.Code.Should().Be(ActiveBoxConflictException.Existing);
        conflict.SavedCandidates.Should().ContainSingle().Which.CartId.Should().Be(box.Box.CartId);
        await using var context = h.Commerce();
        (await context.Carts.CountAsync()).Should().Be(1);
        (await context.CartItems.SingleAsync()).Quantity.Should().Be(1);
        (await context.Carts.SingleAsync()).BoxSize.Should().Be(6);
    }

    [Fact]
    public async Task Create_Should_AllowANewPartyBox_WhenThePreviousOneIsPendingPayment()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        await using var context = h.Commerce();
        var pending = CurrentTestCart(h, f, partyId);
        pending.OrderId = Guid.NewGuid();
        context.Carts.Add(pending);
        await context.SaveChangesAsync();

        var created = await h.BoxCarts().CreateAsync(new(f.BundleProductId, 6, BuyerPartyId: partyId));

        created.Box.CartId.Should().NotBe(pending.Id);
        (await context.Carts.CountAsync()).Should().Be(2);
        (await context.Carts.AsNoTracking().SingleAsync(cart => cart.Id == pending.Id)).OrderId.Should().Be(pending.OrderId);
    }

    [Fact]
    public async Task Create_Should_DetachAnInvalidFirstLineAttempt_BeforeReusingTheService()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyId = Guid.NewGuid();
        var service = h.BoxCarts();
        var invalid = () => service.CreateAsync(new(f.BundleProductId, 6,
            new(f.DishVariants["jollof"], 1, Sel("""{"protein":"wagyu"}""")), partyId));

        await invalid.Should().ThrowAsync<OptionValidationException>();
        var valid = await service.CreateAsync(new(f.BundleProductId, 6,
            new(f.DishVariants["jollof"], 1), partyId));

        await using var context = h.Commerce();
        (await context.Carts.SingleAsync()).Id.Should().Be(valid.Box.CartId);
        (await context.CartItems.CountAsync()).Should().Be(1);
    }

    private static Cart CurrentTestCart(BoxTestHarness h, BoxTestHarness.BoxFixture f, Guid? partyId) => new()
    {
        TenantId = h.TenantId,
        BuyerPartyId = partyId,
        BoxBundleProductId = f.BundleProductId,
        BoxSize = 6,
        Currency = "GBP",
        Status = CartStatuses.Open,
    };
}
