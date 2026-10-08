using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Application.Tests.Commerce;

public partial class BoxCartServiceTests
{
    [Theory]
    [InlineData("size")]
    [InlineData("add")]
    [InlineData("extra")]
    [InlineData("update")]
    [InlineData("remove")]
    [InlineData("continue")]
    public async Task ExplicitBoxActions_Should_RequireAnObservedVersion_BeforeBusinessValidation(string action)
    {
        var h = new BoxTestHarness();
        var catalog = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new(catalog.BundleProductId, 6,
            new(catalog.DishVariants["dish"], 1)));
        var carts = h.BoxCarts();
        var access = CartAccessContext.ForGuest(box.CartToken);
        var lineId = box.Box.Lines.Single().LineId;
        Func<Task> act = async () => await (action switch
        {
            "size" => carts.ChangeSizeAsync(box.Box.CartId, -1, access),
            "add" => carts.AddLineAsync(box.Box.CartId, new(Guid.NewGuid(), 1), access),
            "extra" => carts.AddExtraLineAsync(box.Box.CartId, new(Guid.NewGuid(), 1), access),
            "update" => carts.UpdateLineAsync(box.Box.CartId, lineId, new(Quantity: -1), access),
            "remove" => carts.RemoveLineAsync(box.Box.CartId, lineId, access),
            _ => carts.ContinueAsync(box.Box.CartId, access),
        });

        var error = (await act.Should().ThrowAsync<CartWriteConflictException>()).Which;

        error.CartId.Should().Be(box.Box.CartId);
        error.CartVersion.Should().Be(box.CartVersion);
        error.Code.Should().Be("commerce.cart_conflict");
        await using var read = h.Commerce();
        (await read.CartItems.SingleAsync()).Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task StaleVersion_Should_RejectAnAuthorizedWrite_ButNeverExposeStateToAWrongToken()
    {
        var (h, _, box) = await ArrangeAsync();
        var currentVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await using (var context = h.Commerce())
        {
            var cart = await context.Carts.SingleAsync();
            cart.RowVersion = currentVersion;
            await context.SaveChangesAsync();
        }
        var stale = () => h.BoxCarts().ChangeSizeAsync(box.Box.CartId, 7, Token(box));
        var unauthorized = () => h.BoxCarts().ChangeSizeAsync(box.Box.CartId, 7,
            CartAccessContext.ForGuest("wrong-token"));

        var error = (await stale.Should().ThrowAsync<CartWriteConflictException>()).Which;
        error.CartVersion.Should().Be(Convert.ToBase64String(currentVersion));
        await unauthorized.Should().ThrowAsync<NotFoundException>();
        await using var read = h.Commerce();
        (await read.Carts.SingleAsync()).BoxSize.Should().Be(6);
    }

    [Fact]
    public async Task ReadsAndNoOpActions_Should_PreserveActivity_WhileAMeaningfulEditAdvancesIt()
    {
        var h = new BoxTestHarness();
        var catalog = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new(catalog.BundleProductId, 6,
            new(catalog.DishVariants["dish"], 6)));
        var createdAt = h.Clock.UtcNow;
        h.Clock.UtcNow = createdAt.AddHours(1);
        var carts = h.BoxCarts();
        var access = Token(box);

        var unchanged = await carts.GetAsync(box.Box.CartId, access);
        await carts.QuoteAsync(box.Box.CartId, access);
        await carts.ChangeSizeAsync(box.Box.CartId, 6, access);
        await carts.UpdateLineAsync(box.Box.CartId, unchanged.Box.Lines.Single().LineId,
            new(Quantity: 6, Personalisation: unchanged.Box.Lines.Single().Personalisation), access);
        await carts.ContinueAsync(box.Box.CartId, access);

        await using (var read = h.Commerce())
        {
            var saved = await read.Carts.SingleAsync();
            saved.LastActivityAtUtc.Should().Be(createdAt);
            saved.UpdatedAt.Should().BeNull();
        }
        await carts.ChangeSizeAsync(box.Box.CartId, 7, access);
        await using var edited = h.Commerce();
        (await edited.Carts.SingleAsync()).LastActivityAtUtc.Should().Be(h.Clock.UtcNow);
    }

    [Fact]
    public async Task ReadRepair_Should_PreserveLegacyActivity_WithoutAVisibleChangeNotice()
    {
        var h = new BoxTestHarness();
        var catalog = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new(catalog.BundleProductId, 6,
            new(catalog.DishVariants["dish"], 1)));
        var previousActivity = h.Clock.UtcNow;
        await using (var legacy = h.Commerce())
        {
            var cart = await legacy.Carts.Include(row => row.Items).SingleAsync();
            cart.LastActivityAtUtc = null;
            cart.Items.Single().PersonalisationSummary = "legacy display cache";
            await legacy.SaveChangesAsync();
        }
        h.Clock.UtcNow = previousActivity.AddHours(2);

        var repaired = await h.BoxCarts().GetAsync(box.Box.CartId, Token(box));

        repaired.Changes.Should().BeEmpty("normalizing a display cache need not emit a visible option-change notice");
        await using var read = h.Commerce();
        var saved = await read.Carts.Include(row => row.Items).SingleAsync();
        saved.LastActivityAtUtc.Should().Be(previousActivity);
        saved.UpdatedAt.Should().Be(h.Clock.UtcNow);
        saved.Items.Single().PersonalisationSummary.Should().NotBe("legacy display cache");
    }

    [Fact]
    public async Task CheckoutRepairWithoutAVisibleNotice_Should_ReturnRefreshedStateBeforeInventoryOrPayment()
    {
        var h = new BoxTestHarness();
        var catalog = await h.BuildAsync("dish");
        var box = await h.BoxCarts().CreateAsync(new(catalog.BundleProductId, 6,
            new(catalog.DishVariants["dish"], 6)));
        var originalActivity = h.Clock.UtcNow;
        await using (var legacy = h.Commerce())
        {
            var line = await legacy.CartItems.SingleAsync();
            line.PersonalisationSummary = "legacy display cache";
            await legacy.SaveChangesAsync();
        }
        h.Clock.UtcNow = originalActivity.AddHours(1);
        var checkout = () => h.Checkout().CheckoutAsync(new(box.Box.CartId, "Stripe", "Card",
            Delivery: BoxTestHarness.ValidDelivery), Token(box));

        var error = (await checkout.Should().ThrowAsync<BoxCheckoutDriftException>()).Which;

        error.Refreshed.Changes.Should().BeEmpty();
        error.Refreshed.Status.Should().Be(CartStatuses.Open);
        error.Refreshed.OrderId.Should().BeNull();
        h.Payments.Calls.Should().Be(0);
        await using var read = h.Commerce();
        var saved = await read.Carts.Include(row => row.Items).SingleAsync();
        saved.LastActivityAtUtc.Should().Be(originalActivity);
        saved.UpdatedAt.Should().Be(h.Clock.UtcNow);
        saved.Items.Single().PersonalisationSummary.Should().NotBe("legacy display cache");
        (await read.InventoryReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NativeWriteConflict_Should_NotRetryOrFlushRejectedLines_OnALaterSharedContextSave()
    {
        var (h, catalog, box) = await ArrangeAsync();
        var failure = new FailCartSaveOnce(box.Box.CartId);
        await using var context = h.Commerce(failure);
        var unrelated = new Cart { TenantId = h.TenantId, Currency = "GBP", AnonymousToken = CartAccess.MintToken() };
        context.Carts.Add(unrelated);
        var add = () => h.BoxCarts(context).AddLineAsync(box.Box.CartId,
            new(catalog.DishVariants["jollof"], 1), Token(box));

        await add.Should().ThrowAsync<DbUpdateConcurrencyException>();

        failure.Calls.Should().Be(1, "a failed explicit edit must not be replayed automatically");
        context.Entry(unrelated).State.Should().Be(EntityState.Added);
        context.ChangeTracker.Entries<Cart>().Should().NotContain(entry => entry.Entity.Id == box.Box.CartId);
        context.ChangeTracker.Entries<CartItem>().Should().NotContain(entry => entry.Entity.CartId == box.Box.CartId);
        await context.SaveChangesAsync();
        await using var read = h.Commerce();
        (await read.CartItems.CountAsync(row => row.CartId == box.Box.CartId)).Should().Be(0);
        (await read.Carts.AnyAsync(row => row.Id == unrelated.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task BoxRead_Should_ReturnTheSharedDraftAndRecordedCompletionState()
    {
        var (h, _, box) = await ArrangeAsync();
        var draft = new CartCheckoutDraftDto(Notes: "Leave with reception", CreateAccount: true);
        await h.Carts().SaveCheckoutDraftAsync(box.Box.CartId, draft, Token(box));
        var orderId = Guid.NewGuid();
        await using (var context = h.Commerce())
        {
            var cart = await context.Carts.SingleAsync();
            cart.Status = CartStatuses.CheckedOut;
            cart.OrderId = orderId;
            await context.SaveChangesAsync();
        }

        var result = await h.BoxCarts().GetAsync(box.Box.CartId, Token(box));

        result.CheckoutDraft.Should().Be(draft);
        result.Status.Should().Be(CartStatuses.CheckedOut);
        result.OrderId.Should().Be(orderId);
    }

    private sealed class FailCartSaveOnce(Guid cartId) : SaveChangesInterceptor
    {
        public int Calls { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Cart>().Any(entry => entry.Entity.Id == cartId
                    && entry.State == EntityState.Modified) && Calls++ == 0)
                throw new DbUpdateConcurrencyException("Simulated competing cart write.");
            return ValueTask.FromResult(result);
        }
    }
}
