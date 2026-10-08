using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public class CartRetentionTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false, -1, true)]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, false)]
    [InlineData(true, -1, true)]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, false)]
    public async Task Sweep_Should_UseDishDependentInactivity_AtTheExactBoundary(bool hasDish, long offsetTicks, bool expires)
    {
        var h = new Harness();
        var cart = await h.SeedAsync(Now.AddHours(hasDish ? -168 : -24).AddTicks(offsetTicks), hasDish);

        (await h.Maintenance().FindTenantsWithIdleBoxCartsAsync()).Should()
            .Equal(expires ? [cart.TenantId] : Array.Empty<Guid>());
        (await h.Maintenance().AbandonIdleBoxCartsAsync()).Should().Be(expires ? 1 : 0);

        await using var read = h.Context();
        (await read.Carts.SingleAsync()).Status.Should().Be(expires ? CartStatuses.Abandoned : CartStatuses.Open);
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("removed")]
    [InlineData("zero")]
    [InlineData("foreign")]
    [InlineData("no-slot")]
    public async Task Sweep_Should_TreatOnlyRetainedOwnedDishUnitsAsPopulated(string lineCase)
    {
        var h = new Harness();
        var cart = await h.SeedAsync(Now.AddDays(-2));
        await using (var seed = h.Context())
        {
            seed.CartItems.Add(new CartItem
            {
                TenantId = lineCase == "foreign" ? Guid.NewGuid() : cart.TenantId,
                CartId = cart.Id, ProductVariantId = Guid.NewGuid(),
                LineKind = lineCase == "extra" ? CartLineKinds.AddOn : CartLineKinds.BoxDish,
                BoxBundleSlotId = lineCase == "no-slot" ? null : Guid.NewGuid(),
                Quantity = lineCase == "zero" ? 0 : 1,
                IsDeleted = lineCase == "removed"
            });
            await seed.SaveChangesAsync();
        }

        (await h.Maintenance().AbandonIdleBoxCartsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Sweep_Should_RetainUnavailableDishes_WithoutConsultingTheLiveCatalogue()
    {
        var h = new Harness();
        await h.SeedAsync(Now.AddDays(-2), hasDish: true); // Product is absent; customer work remains.

        (await h.Maintenance().AbandonIdleBoxCartsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sweep_Should_UseExplicitActivity_DespiteNewerAuditWrites_AndKeepLegacyFallback()
    {
        var h = new Harness();
        var oldActivity = await h.SeedAsync(Now.AddDays(-2));
        var recentActivity = await h.SeedAsync(Now.AddHours(-1));
        var legacy = await h.SeedAsync(Now.AddDays(-2));
        await using (var edit = h.Context())
        {
            var first = await edit.Carts.FindAsync(oldActivity.Id);
            first!.UpdatedAt = Now; // Server audit changes must not renew explicit activity.
            var recent = await edit.Carts.FindAsync(recentActivity.Id);
            recent!.CreatedAt = Now.AddDays(-30);
            await edit.SaveChangesAsync();
        }
        await using (var edit = h.Context())
        {
            var old = await edit.Carts.FindAsync(legacy.Id);
            old!.LastActivityAtUtc = null;
            h.Clock.UtcNow = Now.AddDays(-2);
            await edit.SaveChangesAsync();
        }
        h.Clock.UtcNow = Now;

        (await h.Maintenance().AbandonIdleBoxCartsAsync()).Should().Be(2);
        await using var read = h.Context();
        (await read.Carts.SingleAsync(c => c.Id == recentActivity.Id)).Status.Should().Be(CartStatuses.Open);
        (await read.Carts.SingleAsync(c => c.Id == legacy.Id)).LastActivityAtUtc.Should().Be(Now.AddDays(-2));
    }

    [Theory]
    [InlineData("checked-out")]
    [InlineData("abandoned")]
    [InlineData("deleted")]
    [InlineData("generic")]
    [InlineData("pending")]
    public async Task Sweep_Should_ExcludeNoneditableOrNonBoxCarts(string state)
    {
        var h = new Harness();
        var cart = await h.SeedAsync(Now.AddDays(-60));
        await using (var edit = h.Context())
        {
            var row = await edit.Carts.FindAsync(cart.Id);
            row!.Status = state switch
            {
                "checked-out" => CartStatuses.CheckedOut,
                "abandoned" => CartStatuses.Abandoned,
                _ => CartStatuses.Open
            };
            row.IsDeleted = state == "deleted";
            if (state == "generic") row.BoxBundleProductId = null;
            if (state == "pending") row.OrderId = Guid.NewGuid();
            await edit.SaveChangesAsync();
        }

        (await h.Maintenance().FindTenantsWithIdleBoxCartsAsync()).Should().BeEmpty();
        (await h.Maintenance().AbandonIdleBoxCartsAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(null, null, 2)]
    [InlineData("14", "72", 0)]
    [InlineData("14", "24", 1)]
    [InlineData("7", "72", 1)]
    [InlineData("0", "0", 2)]
    [InlineData("-1", "-1", 2)]
    [InlineData("366", "8761", 2)]
    [InlineData("2147483647", "2147483647", 2)]
    [InlineData("invalid", "invalid", 2)]
    public async Task Sweep_Should_RespectIndependentGlobalWindows_AndBoundInvalidConfiguration(string? days, string? hours, int expected)
    {
        var h = new Harness();
        h.Settings[CommerceSettingNames.CartsAbandonAfterDays] = days;
        h.Settings[CommerceSettingNames.CartsEmptyAbandonAfterHours] = hours;
        await h.SeedAsync(Now.AddDays(-2));
        await h.SeedAsync(Now.AddDays(-8), hasDish: true);

        (await h.Maintenance().AbandonIdleBoxCartsAsync()).Should().Be(expected);
    }

    [Fact]
    public async Task Sweep_Should_RespectTenantScope_AndRestoreTheOriginalAmbient()
    {
        var h = new Harness();
        var first = await h.SeedAsync(Now.AddDays(-2));
        var secondTenant = Guid.NewGuid();
        h.Ambient.TenantId = secondTenant;
        var second = await h.SeedAsync(Now.AddDays(-2));
        var originalTenant = Guid.NewGuid();
        h.Ambient.TenantId = originalTenant;
        h.Ambient.ResolutionSource = "original";
        using var context = h.Context();
        var maintenance = h.Maintenance(context);

        (await maintenance.FindTenantsWithIdleBoxCartsAsync()).Should().BeEquivalentTo([first.TenantId, secondTenant]);
        (await maintenance.AbandonIdleBoxCartsAsync(tenantIds: [])).Should().Be(0);
        (await maintenance.AbandonIdleBoxCartsAsync(tenantIds: [first.TenantId])).Should().Be(1);
        h.Ambient.TenantId.Should().Be(originalTenant);
        h.Ambient.ResolutionSource.Should().Be("original");
        h.Ambient.TenantId = secondTenant;
        (await context.Carts.SingleAsync(c => c.Id == second.Id)).Status.Should().Be(CartStatuses.Open);
    }

    [Fact]
    public async Task FailedSweep_Should_RestoreAmbient_AndDetachItsRejectedWrite()
    {
        var h = new Harness();
        await h.SeedAsync(Now.AddDays(-2));
        var originalTenant = Guid.NewGuid();
        h.Ambient.TenantId = originalTenant;
        h.Ambient.ResolutionSource = "original";
        using var context = h.Context(new RejectSave());
        var act = () => h.Maintenance(context).AbandonIdleBoxCartsAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        h.Ambient.TenantId.Should().Be(originalTenant);
        h.Ambient.ResolutionSource.Should().Be("original");
        context.ChangeTracker.Entries<Cart>().Should().BeEmpty();
    }

    private sealed class Harness
    {
        private readonly DbContextOptions<CommerceDbContext> _options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"CartRetention_{Guid.NewGuid()}").Options;
        public MutableClock Clock { get; } = new();
        public TenantContext Ambient { get; } = new() { TenantId = Guid.NewGuid() };
        public Dictionary<string, string?> Settings { get; } = [];

        public CommerceDbContext Context(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<CommerceDbContext>(_options);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new(options.Options, new HttpContextTenantProvider(Ambient), new TestCurrentUserProvider(), Clock);
        }

        public CartMaintenanceService Maintenance(CommerceDbContext? context = null)
        {
            var settings = new Mock<ISettingProvider>(MockBehavior.Strict);
            settings.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string key, CancellationToken _) => Task.FromResult(Settings.GetValueOrDefault(key)));
            return new(context ?? Context(), Ambient, settings.Object, Clock);
        }

        public async Task<Cart> SeedAsync(DateTime activity, bool hasDish = false)
        {
            Clock.UtcNow = activity;
            using var context = Context();
            var cart = new Cart
            {
                TenantId = Ambient.TenantId!.Value, Currency = "GBP", BoxBundleProductId = Guid.NewGuid(),
                BoxSize = 6, AnonymousToken = CartAccess.MintToken(), LastActivityAtUtc = activity
            };
            if (hasDish) cart.Items.Add(new CartItem
            {
                TenantId = cart.TenantId, ProductVariantId = Guid.NewGuid(), Quantity = 1,
                LineKind = CartLineKinds.BoxDish, BoxBundleSlotId = Guid.NewGuid()
            });
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            Clock.UtcNow = Now;
            return cart;
        }
    }

    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } = Now;
    }

    private sealed class RejectSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Test save failure.");
    }
}
