using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Infrastructure.Multitenancy;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Database.Tests.Commerce;

public class CartDraftConcurrencySqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly FixedClock Clock = new();

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameVersionDraftAndRivalWrite_Should_CommitOnlyOne_AndDiscardTheLosingGraph(bool rivalAddsLine)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId);
        var barrier = new TwoCartSaves();
        await using var contextA = Context(tenantId, barrier);
        await using var contextB = Context(tenantId, barrier);
        var access = CartAccessContext.ForGuest(seeded.Token, seeded.Version);
        var first = Carts(contextA, tenantId).SaveCheckoutDraftAsync(seeded.CartId,
            new CartCheckoutDraftDto(Notes: "First tab"), access);
        Task second = rivalAddsLine
            ? Carts(contextB, tenantId).AddItemAsync(new AddCartItemCommand(seeded.CartId, seeded.VariantId), access)
            : Carts(contextB, tenantId).SaveCheckoutDraftAsync(seeded.CartId,
                new CartCheckoutDraftDto(Notes: "Second tab"), access);

        var failures = await Task.WhenAll(Capture(first), Capture(second));

        failures.Count(error => error is null).Should().Be(1);
        failures.Single(error => error is not null).Should().BeOfType<DbUpdateConcurrencyException>();
        // A later save on either scope must not retry the losing child or draft mutation.
        await contextA.SaveChangesAsync();
        await contextB.SaveChangesAsync();
        await using var verify = Context(tenantId);
        var cart = await verify.Carts.Include(row => row.Items).SingleAsync(row => row.Id == seeded.CartId);
        Convert.ToBase64String(cart.RowVersion).Should().NotBe(seeded.Version);
        cart.LastActivityAtUtc.Should().Be(Clock.UtcNow);
        var note = CartDraftData.Read(cart)?.Notes;
        if (rivalAddsLine)
        {
            if (failures[0] is null)
            {
                note.Should().Be("First tab");
                cart.Items.Should().BeEmpty();
            }
            else
            {
                note.Should().BeNull();
                cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(1m);
            }
        }
        else
        {
            note.Should().Be(failures[0] is null ? "First tab" : "Second tab");
        }
    }

    [SkippableFact]
    public async Task GuestEditResponse_Should_NotReadPrivateDraft_AfterItsCommittedWriteIsAdopted()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId);
        var gate = new AfterCartSave(seeded.CartId);
        await using var guestContext = Context(tenantId, gate);
        var guest = Carts(guestContext, tenantId).AddItemAsync(
            new AddCartItemCommand(seeded.CartId, seeded.VariantId),
            CartAccessContext.ForGuest(seeded.Token, seeded.Version));
        try
        {
            await gate.WaitUntilReachedAsync(guest);
            await using var ownerContext = Context(tenantId);
            var ownerCarts = Carts(ownerContext, tenantId);
            var committed = await ownerCarts.GetCartAsync(seeded.CartId, CartAccessContext.ForGuest(seeded.Token));
            committed!.Items.Should().ContainSingle();
            var adopted = await ownerCarts.AdoptAsync(seeded.CartId, partyId,
                CartAccessContext.ForGuest(seeded.Token, committed.CartVersion));
            var privateDraft = new CartCheckoutDraftDto(
                Purchaser: new CheckoutContactDto("owner@example.test", "Private", "Owner", "07123456789"),
                Notes: "Owner-only delivery instructions");
            var saved = await ownerCarts.SaveCheckoutDraftAsync(seeded.CartId, privateDraft,
                CartAccessContext.ForParty(partyId, adopted.CartVersion));
            gate.Release.TrySetResult();

            Func<Task> response = async () => await guest.WaitAsync(TimeSpan.FromSeconds(30));
            await response.Should().ThrowAsync<NotFoundException>();
            await guestContext.SaveChangesAsync();
            await using var verify = Context(tenantId);
            var cart = await verify.Carts.Include(row => row.Items).SingleAsync(row => row.Id == seeded.CartId);
            cart.BuyerPartyId.Should().Be(partyId);
            cart.AnonymousToken.Should().BeNull();
            cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(1m);
            CartDraftData.Read(cart).Should().Be(privateDraft);
            Convert.ToBase64String(cart.RowVersion).Should().Be(saved.CartVersion);
        }
        finally
        {
            gate.Release.TrySetResult();
            await Capture(guest).WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [SkippableFact]
    public async Task ReusedContext_Should_RejectAnObservedStaleDraft_AndRefreshItsTrackedGraph()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId);
        await using var staleContext = Context(tenantId);
        var tracked = await staleContext.Carts.SingleAsync(row => row.Id == seeded.CartId);
        tracked.CheckoutDraftJson = CartDraftData.Serialize(new CartCheckoutDraftDto(Notes: "Uncommitted ghost"));
        staleContext.CartItems.Add(new CartItem
        {
            TenantId = tenantId, CartId = seeded.CartId, ProductVariantId = seeded.VariantId,
            Quantity = 99, UnitPriceSnapshot = 10m
        });
        await using (var writer = Context(tenantId))
        {
            await Carts(writer, tenantId).SaveCheckoutDraftAsync(seeded.CartId,
                new CartCheckoutDraftDto(Notes: "Saved in another tab"), CartAccessContext.ForGuest(seeded.Token, seeded.Version));
        }

        var act = () => Carts(staleContext, tenantId).SaveCheckoutDraftAsync(seeded.CartId,
            new CartCheckoutDraftDto(Notes: "Stale overwrite"), CartAccessContext.ForGuest(seeded.Token, seeded.Version));

        await act.Should().ThrowAsync<CartWriteConflictException>();
        await staleContext.SaveChangesAsync();
        await using var verify = Context(tenantId);
        var persisted = await verify.Carts.Include(row => row.Items).SingleAsync();
        CartDraftData.Read(persisted)!.Notes.Should().Be("Saved in another tab");
        persisted.Items.Should().BeEmpty();
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SweepRacingWithActivityOrCheckout_Should_SkipTheFreshCart_AndContinueWithOtherIdleWork(bool checkout)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var race = await SeedAsync(tenantId, box: true, lastActivity: Clock.UtcNow.AddDays(-2));
        var other = await SeedAsync(tenantId, box: true, lastActivity: Clock.UtcNow.AddDays(-2));
        var populated = await SeedAsync(tenantId, box: true, lastActivity: Clock.UtcNow.AddDays(-2));
        await using (var seed = Context(tenantId))
        {
            seed.CartItems.Add(new CartItem
            {
                TenantId = tenantId, CartId = populated.CartId, ProductVariantId = populated.VariantId,
                BoxBundleSlotId = Guid.NewGuid(), LineKind = CartLineKinds.BoxDish, Quantity = 1
            });
            await seed.SaveChangesAsync();
        }
        var ambient = new TenantContext { TenantId = Guid.NewGuid(), ResolutionSource = "before-sweep" };
        var originalTenant = ambient.TenantId;
        var gate = new BeforeAbandonSave(race.CartId);
        var options = new DbContextOptionsBuilder<CommerceDbContext>(database.CreateOptions<CommerceDbContext>())
            .AddInterceptors(gate).Options;
        await using var sweepContext = new CommerceDbContext(options, new HttpContextTenantProvider(ambient), new TestCurrentUserProvider(), Clock);
        var maintenance = new CartMaintenanceService(sweepContext, ambient, Mock.Of<ISettingProvider>(), Clock);
        var sweep = maintenance.AbandonIdleBoxCartsAsync();
        try
        {
            await gate.WaitUntilReachedAsync(sweep);
            await using (var writer = Context(tenantId))
            {
                if (checkout)
                {
                    // Checkout's durable claim; pending payment deliberately leaves Status Open.
                    var cart = await writer.Carts.SingleAsync(row => row.Id == race.CartId);
                    cart.OrderId = Guid.NewGuid();
                    await writer.SaveChangesAsync();
                }
                else
                {
                    await Carts(writer, tenantId).SaveCheckoutDraftAsync(race.CartId,
                        new CartCheckoutDraftDto(Notes: "Still ordering"), CartAccessContext.ForGuest(race.Token, race.Version));
                }
            }
            gate.Release.TrySetResult();

            (await sweep).Should().Be(1, "only the unrelated stale empty box may be abandoned");
            ambient.TenantId.Should().Be(originalTenant);
            ambient.ResolutionSource.Should().Be("before-sweep");
            await sweepContext.SaveChangesAsync(); // No stale abandonment can be re-flushed.
            await using var verify = Context(tenantId);
            var fresh = await verify.Carts.SingleAsync(row => row.Id == race.CartId);
            fresh.Status.Should().Be(CartStatuses.Open);
            (await verify.Carts.SingleAsync(row => row.Id == other.CartId)).Status.Should().Be(CartStatuses.Abandoned);
            (await verify.Carts.SingleAsync(row => row.Id == populated.CartId)).Status.Should().Be(CartStatuses.Open);
            if (checkout) fresh.OrderId.Should().NotBeNull();
            else
            {
                fresh.LastActivityAtUtc.Should().Be(Clock.UtcNow);
                CartDraftData.Read(fresh)!.Notes.Should().Be("Still ordering");
            }
            (await maintenance.AbandonIdleBoxCartsAsync()).Should().Be(0);
        }
        finally { gate.Release.TrySetResult(); }
    }

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server LocalDB unavailable.");

    private CommerceDbContext Context(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>(database.CreateOptions<CommerceDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
    }

    private static CartService Carts(CommerceDbContext context, Guid tenantId)
        => new(context, new TestTenantProvider(tenantId), new ProductPricingService(context, new TestTenantProvider(tenantId), Clock), Clock);

    private async Task<(Guid CartId, string Token, string Version, Guid VariantId)> SeedAsync(
        Guid tenantId, bool box = false, DateTime? lastActivity = null)
    {
        await using var context = Context(tenantId);
        var product = new Product { TenantId = tenantId, Slug = Guid.NewGuid().ToString("N"), Name = "Dish", Status = ProductStatuses.Active };
        var variant = new ProductVariant { TenantId = tenantId, ProductId = product.Id, Sku = Guid.NewGuid().ToString("N"), Name = "Regular" };
        var cart = new Cart
        {
            TenantId = tenantId, AnonymousToken = CartAccess.MintToken(), Currency = "GBP",
            BoxBundleProductId = box ? product.Id : null, BoxSize = box ? 6 : null,
            LastActivityAtUtc = lastActivity ?? Clock.UtcNow.AddHours(-1)
        };
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        context.ProductPrices.Add(new ProductPrice { TenantId = tenantId, ProductVariantId = variant.Id, Currency = "GBP", Amount = 10m });
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
        return (cart.Id, cart.AnonymousToken, Convert.ToBase64String(cart.RowVersion), variant.Id);
    }

    private static async Task<Exception?> Capture(Task task)
    {
        try { await task; return null; }
        catch (Exception error) { return error; }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TwoCartSaves : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Cart>().Any(entry => entry.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class AfterCartSave(Guid cartId) : SaveChangesInterceptor
    {
        private int _seen;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitUntilReachedAsync(Task operation)
        {
            if (await Task.WhenAny(Reached.Task, operation).WaitAsync(TimeSpan.FromSeconds(30)) == operation)
            {
                await operation;
                throw new InvalidOperationException("Guest edit completed before its committed save was observed.");
            }
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (result > 0 && eventData.Context!.ChangeTracker.Entries<Cart>().Any(entry => entry.Entity.Id == cartId)
                && Interlocked.CompareExchange(ref _seen, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class BeforeAbandonSave(Guid cartId) : SaveChangesInterceptor
    {
        private int _seen;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitUntilReachedAsync(Task operation)
        {
            if (await Task.WhenAny(Reached.Task, operation).WaitAsync(TimeSpan.FromSeconds(30)) == operation)
            {
                await operation;
                throw new InvalidOperationException("Sweep completed before its abandonment save.");
            }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Cart>().Any(entry => entry.Entity.Id == cartId
                && entry.State == EntityState.Modified && entry.Entity.Status == CartStatuses.Abandoned)
                && Interlocked.CompareExchange(ref _seen, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
