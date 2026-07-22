using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Spec 072 — guest→account adoption (Y4, Z2–Z4) and party-scoped order reads (Y5, Z5).</summary>
public class StorefrontIdentityTests
{
    private static CartAccessContext Token(BoxCartDto dto) => CartAccessContext.ForGuest(dto.CartToken);

    private static async Task<(BoxTestHarness H, BoxTestHarness.BoxFixture F, BoxCartDto Box)> GuestBoxAsync()
    {
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        return (h, f, box);
    }

    [Fact]
    public async Task C1_Adoption_BindsTheParty_AndKillsTheGuestToken()
    {
        var (h, _, box) = await GuestBoxAsync();
        var party = Guid.NewGuid();

        var adopted = await h.Carts().AdoptAsync(box.Box.CartId, party, Token(box));
        adopted.BuyerPartyId.Should().Be(party);

        // Z3 — the leaked pre-adoption token is dead; the party principal now authorizes.
        var viaToken = () => h.BoxCarts().GetAsync(box.Box.CartId, Token(box));
        await viaToken.Should().ThrowAsync<NotFoundException>();
        var viaParty = await h.BoxCarts().GetAsync(box.Box.CartId, CartAccessContext.ForParty(party));
        viaParty.Box.CartId.Should().Be(box.Box.CartId);

        // Idempotent for the owning party.
        var again = await h.Carts().AdoptAsync(box.Box.CartId, party, CartAccessContext.ForGuest(null));
        again.BuyerPartyId.Should().Be(party);
    }

    [Fact]
    public async Task C2_Adoption_FailsClosed_OnWrongTokenOrForeignParty()
    {
        var (h, _, box) = await GuestBoxAsync();

        var wrongToken = () => h.Carts().AdoptAsync(box.Box.CartId, Guid.NewGuid(),
            CartAccessContext.ForGuest(new string('x', 47)));
        await wrongToken.Should().ThrowAsync<NotFoundException>("possession is half the requirement");

        await h.Carts().AdoptAsync(box.Box.CartId, Guid.NewGuid(), Token(box));
        var foreign = () => h.Carts().AdoptAsync(box.Box.CartId, Guid.NewGuid(), Token(box));
        await foreign.Should().ThrowAsync<NotFoundException>("a cart bound to another party is invisible (Z2)");
    }

    [Fact]
    public async Task C5_Adoption_Rejects_AfterCheckoutStampsTheOrder()
    {
        var (h, _, box) = await GuestBoxAsync();
        await using (var ctx = h.Commerce())
        {
            var cart = await ctx.Carts.FirstAsync(c => c.Id == box.Box.CartId);
            cart.OrderId = Guid.NewGuid();
            await ctx.SaveChangesAsync();
        }

        var act = () => h.Carts().AdoptAsync(box.Box.CartId, Guid.NewGuid(), Token(box));

        (await act.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("Z4");
    }

    [Fact]
    public async Task C5b_Adoption_Rejects_ForTheOwningParty_OnceOrdered()
    {
        // The owner's idempotent retry must not outrank Z4: adopt, check out, retry the adopt.
        var (h, _, box) = await GuestBoxAsync();
        var party = Guid.NewGuid();
        await h.Carts().AdoptAsync(box.Box.CartId, party, Token(box));
        await using (var ctx = h.Commerce())
        {
            var cart = await ctx.Carts.FirstAsync(c => c.Id == box.Box.CartId);
            cart.OrderId = Guid.NewGuid();
            await ctx.SaveChangesAsync();
        }

        var retry = () => h.Carts().AdoptAsync(box.Box.CartId, party, CartAccessContext.ForGuest(null));

        (await retry.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("Z4");
    }

    [Fact]
    public async Task C4_MyOrders_AreScopedToTheParty()
    {
        // Two customers each check out a full box; each sees exactly their own order.
        var h = new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var partyA = Guid.NewGuid();
        var partyB = Guid.NewGuid();
        // Two 6-boxes; the first checkout's reservation holds 6 of the default 10.
        await h.Inventory().SetOnHandAsync(f.DishVariants["jollof"], 20m);

        async Task<Guid> CheckoutFor(Guid party)
        {
            var carts = h.BoxCarts();
            var box = await carts.CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6, BuyerPartyId: party));
            var access = CartAccessContext.ForParty(party);
            await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 6, null), access);
            var result = await h.Checkout().CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), access);
            return result.OrderId;
        }

        var orderA1 = await CheckoutFor(partyA);
        var orderA2 = await CheckoutFor(partyA);
        var orderB = await CheckoutFor(partyB);

        var orders = new StorefrontOrderService(h.Commerce(),
            new Aonik.TestSupport.Multitenancy.TestTenantProvider(h.TenantId),
            new Aonik.Ordering.Services.CoreOrderService(h.Ordering(),
                new Aonik.TestSupport.Multitenancy.TestTenantProvider(h.TenantId),
                new CommerceTestHarness.TestClock(),
                new Aonik.TestSupport.Identity.TestCurrentUserProvider()));

        var mine = await orders.ListMyOrdersAsync(partyA);
        mine.TotalCount.Should().Be(2, "only the caller's own orders list");
        mine.Items.Select(o => o.OrderId).Should().BeEquivalentTo([orderA1, orderA2]);
        mine.Items.Should().NotContain(o => o.OrderId == orderB);
        mine.Items.Should().OnlyContain(o => o.BoxSize == 6 && o.Total == 95m);

        // Paging is real: two pages of one, disjoint, same total.
        var page1 = await orders.ListMyOrdersAsync(partyA, page: 1, pageSize: 1);
        var page2 = await orders.ListMyOrdersAsync(partyA, page: 2, pageSize: 1);
        page1.TotalCount.Should().Be(2);
        page1.Items.Should().ContainSingle();
        page2.Items.Should().ContainSingle();
        page1.Items.Single().OrderId.Should().NotBe(page2.Items.Single().OrderId);

        (await orders.GetMyOrderAsync(partyA, orderB)).Should().BeNull("Z5 — a foreign order id is a 404, never a 403");
        var detail = await orders.GetMyOrderAsync(partyA, orderA1);
        detail!.Items.Should().NotBeEmpty();
        detail.Selections.Should().NotBeEmpty("the box's kitchen landing rides the detail");
    }
}
