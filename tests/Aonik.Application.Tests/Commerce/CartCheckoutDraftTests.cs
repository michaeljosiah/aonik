using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Application.Tests.Commerce;

public class CartCheckoutDraftTests
{
    [Fact]
    public async Task Save_Should_ResumeIncompleteFields_AndClearThemByReplacement()
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var access = CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion);
        var draft = new CartCheckoutDraftDto(Purchaser: new("unfinished@", " Ada ", "", ""),
            Address: new(" 1 Test Road ", " ", "", null, " sw1a ", "g"),
            Notes: " Ring bell\nthen wait ", CreateAccount: true, DiscountCode: " SAVE ");

        var saved = await h.Carts().SaveCheckoutDraftAsync(cart.Id, draft, access);
        var resumed = await h.Carts().GetCartAsync(cart.Id, access);

        resumed!.CheckoutDraft.Should().Be(saved.Draft);
        saved.Draft!.Purchaser!.Email.Should().Be("unfinished@");
        saved.Draft.Purchaser.FirstName.Should().Be("Ada");
        saved.Draft.Address!.CountryCode.Should().Be("G");
        saved.Draft.Address.Line2.Should().BeNull();
        saved.Draft.DiscountCode.Should().Be("SAVE");
        resumed.AnonymousToken.Should().BeNull();

        var cleared = await h.Carts().SaveCheckoutDraftAsync(cart.Id, new(), access);
        cleared.Draft.Should().BeNull();
        (await h.Carts().GetCartAsync(cart.Id, access))!.CheckoutDraft.Should().BeNull();
    }

    [Fact]
    public async Task Save_Should_RefreshActivityOnlyForMeaningfulChanges()
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var access = CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion);
        var created = h.Clock.UtcNow;
        h.Clock.UtcNow = created.AddHours(1);
        await h.Carts().SaveCheckoutDraftAsync(cart.Id, new(), access);
        await using (var context = h.Commerce())
            (await context.Carts.SingleAsync()).LastActivityAtUtc.Should().Be(created);

        var draft = new CartCheckoutDraftDto(Notes: "Ring bell");
        await h.Carts().SaveCheckoutDraftAsync(cart.Id, draft, access);
        var edited = h.Clock.UtcNow;
        h.Clock.UtcNow = edited.AddHours(1);
        await h.Carts().GetCartAsync(cart.Id, access);
        await h.Carts().SaveCheckoutDraftAsync(cart.Id, draft with { Notes = " Ring bell " }, access);
        await using var verify = h.Commerce();
        (await verify.Carts.SingleAsync()).LastActivityAtUtc.Should().Be(edited);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-version")]
    [InlineData("AQ==")]
    [InlineData(" ")]
    public async Task Save_Should_RejectMissingOrStaleVersionsBeforeDraftValidation(string? version)
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var attempt = () => h.Carts().SaveCheckoutDraftAsync(cart.Id, new(Notes: new string('x', 1001)),
            CartAccessContext.ForGuest(cart.AnonymousToken, version));

        var conflict = (await attempt.Should().ThrowAsync<CartWriteConflictException>()).Which;
        conflict.Code.Should().Be("commerce.cart_conflict");
        conflict.CartVersion.Should().Be(cart.CartVersion);
        await using var context = h.Commerce();
        (await context.Carts.SingleAsync()).CheckoutDraftJson.Should().BeNull();
    }

    [Theory]
    [InlineData("length")]
    [InlineData("control")]
    public async Task Save_Should_RejectInvalidFieldsWithoutChangingActivity(string invalid)
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var created = h.Clock.UtcNow;
        h.Clock.UtcNow = created.AddHours(1);
        var attempt = () => h.Carts().SaveCheckoutDraftAsync(cart.Id,
            new(Notes: invalid == "length" ? new string('x', 1001) : "hello\u0000there"),
            CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion));

        await attempt.Should().ThrowAsync<StorefrontValidationException>();
        await using var context = h.Commerce();
        var row = await context.Carts.SingleAsync();
        row.CheckoutDraftJson.Should().BeNull();
        row.LastActivityAtUtc.Should().Be(created);
    }

    [Fact]
    public async Task Save_Should_FreshlyAuthorizeEvenWhenTheOldGuestCartIsTracked()
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        await using var context = h.Commerce();
        await context.Carts.SingleAsync();
        var service = new CartService(context, new TestTenantProvider(h.TenantId), h.Pricing(), h.Clock, h.DiscountQuotes(context));
        await using (var other = h.Commerce())
        {
            var row = await other.Carts.SingleAsync();
            row.BuyerPartyId = Guid.NewGuid();
            row.AnonymousToken = null;
            await other.SaveChangesAsync();
        }
        var attempt = () => service.SaveCheckoutDraftAsync(cart.Id, new(Notes: "private"),
            CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion));

        await attempt.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_Should_ReturnLockedState_ForPendingAndCompletedCheckout(bool paid)
    {
        var h = new BoxTestHarness();
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        var orderId = Guid.NewGuid();
        await using (var context = h.Commerce())
        {
            var row = await context.Carts.SingleAsync();
            row.OrderId = orderId;
            row.Status = paid ? CartStatuses.CheckedOut : CartStatuses.Open;
            await context.SaveChangesAsync();
        }
        var attempt = () => h.Carts().SaveCheckoutDraftAsync(cart.Id, new(Notes: "change"),
            CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion));

        var conflict = (await attempt.Should().ThrowAsync<CartWriteConflictException>()).Which;
        conflict.Code.Should().Be("commerce.cart_locked");
        conflict.OrderId.Should().Be(orderId);
        conflict.Status.Should().Be(paid ? CartStatuses.CheckedOut : CartStatuses.Open);
    }

    [Fact]
    public async Task FailedSave_Should_NotBeFlushedByLaterInventoryWorkInTheSameContext()
    {
        var h = new BoxTestHarness();
        var fixture = await h.BuildAsync("dish");
        var cart = await h.Carts().CreateCartAsync(new("GBP"));
        await using var context = h.Commerce(new RejectDraftSave());
        var tenant = new TestTenantProvider(h.TenantId);
        var service = new CartService(context, tenant, h.Pricing(), h.Clock, h.DiscountQuotes(context));
        var attempt = () => service.SaveCheckoutDraftAsync(cart.Id, new(Notes: "rejected"),
            CartAccessContext.ForGuest(cart.AnonymousToken, cart.CartVersion));
        await attempt.Should().ThrowAsync<DbUpdateConcurrencyException>();

        var inventory = new InventoryService(context, tenant, new TenantContext { TenantId = h.TenantId }, h.Clock);
        await inventory.SetOnHandAsync(fixture.DishVariants["dish"], 20);

        await using var verify = h.Commerce();
        (await verify.Carts.SingleAsync()).CheckoutDraftJson.Should().BeNull();
        (await inventory.GetAvailableAsync(fixture.DishVariants["dish"])).Should().Be(20);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Checkout_Should_UseSavedDraft_UnlessCompleteExplicitDeliveryIsSupplied(bool explicitDelivery)
    {
        var h = new BoxTestHarness();
        var fixture = await h.BuildAsync("dish");
        var created = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6,
            new(fixture.DishVariants["dish"], 6)));
        var access = CartAccessContext.ForGuest(created.CartToken, created.CartVersion);
        var details = BoxTestHarness.ValidDelivery with { Notes = "from draft" };
        await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, new(details.Purchaser, details.Address,
            DeliveryDate: details.DeliveryDate, Notes: details.Notes, CreateAccount: true), access);
        var explicitDetails = details with { Notes = "explicit", Address = details.Address with { Line1 = "2 Other Road" } };

        var result = await h.Checkout().CheckoutAsync(new(created.Box.CartId, "Stripe", "Card",
            Delivery: explicitDelivery ? explicitDetails : null), access);

        await using var context = h.Commerce();
        var snapshot = await context.OrderDeliveryDetails.SingleAsync();
        snapshot.OrderId.Should().Be(result.OrderId);
        snapshot.Notes.Should().Be(explicitDelivery ? "explicit" : "from draft");
        snapshot.AddressLine1.Should().Be(explicitDelivery ? "2 Other Road" : details.Address.Line1);
        var replay = await h.Checkout().CheckoutAsync(new(created.Box.CartId, "", ""),
            CartAccessContext.ForGuest(created.CartToken));
        replay.OrderId.Should().Be(result.OrderId);
        h.Payments.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Checkout_Should_RejectIncompleteSavedDeliveryAndMissingGiftRecipient_BeforePayment()
    {
        var h = new BoxTestHarness();
        var fixture = await h.BuildAsync("dish");
        var created = await h.BoxCarts().CreateAsync(new(fixture.BundleProductId, 6,
            new(fixture.DishVariants["dish"], 6)));
        var access = CartAccessContext.ForGuest(created.CartToken, created.CartVersion);
        await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, new(Purchaser: new("unfinished@", "", "", "")), access);
        var incomplete = () => h.Checkout().CheckoutAsync(new(created.Box.CartId, "Stripe", "Card"), access);
        await incomplete.Should().ThrowAsync<StorefrontValidationException>();
        await h.Carts().SaveCheckoutDraftAsync(created.Box.CartId, new(Gift: new(GiftIntent: true, IncludeGreetingCard: true)), access);
        var gift = () => h.Checkout().CheckoutAsync(new(created.Box.CartId, "Stripe", "Card", Delivery: BoxTestHarness.ValidDelivery), access);
        await gift.Should().ThrowAsync<StorefrontValidationException>().WithMessage("Recipient:*");
        h.Payments.Calls.Should().Be(0);
        await using var context = h.Commerce();
        (await context.InventoryReservations.AnyAsync()).Should().BeFalse();
    }

    private sealed class RejectDraftSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Cart>().Any(e => e.Entity.CheckoutDraftJson is not null))
                throw new DbUpdateConcurrencyException("A competing cart edit won.");
            return ValueTask.FromResult(result);
        }
    }
}
