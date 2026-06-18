using Aonik.Commerce.Services.Inventory;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Inventory reservation lifecycle (Spec 042 §10): reserve, commit, release, expiry.</summary>
public class InventoryServiceTests
{
    private static (InventoryService Service, CommerceTestHarness.TestClock Clock, Guid Tenant, Aonik.Commerce.Persistence.CommerceDbContext Ctx) Build(
        Microsoft.EntityFrameworkCore.DbContextOptions<Aonik.Commerce.Persistence.CommerceDbContext> options, Guid tenantId)
    {
        var clock = new CommerceTestHarness.TestClock();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var svc = new InventoryService(ctx, new TestTenantProvider(tenantId), clock);
        return (svc, clock, tenantId, ctx);
    }

    [Fact]
    public async Task Reserve_Then_GetAvailable_Should_DecrementAvailable()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        var cart = Guid.NewGuid();
        await svc.SetOnHandAsync(variant, 10m);

        await svc.ReserveAsync(cart, new[] { new InventoryReservationLine(variant, 3m) });

        (await svc.GetAvailableAsync(variant)).Should().Be(7m);
    }

    [Fact]
    public async Task Reserve_Should_Throw_WhenInsufficientStock()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        await svc.SetOnHandAsync(variant, 2m);

        var act = async () => await svc.ReserveAsync(Guid.NewGuid(), new[] { new InventoryReservationLine(variant, 3m) });

        await act.Should().ThrowAsync<InsufficientStockException>();
    }

    [Fact]
    public async Task Reserve_Should_BeAllOrNothing_WhenOneLineCannotBeSatisfied()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();
        await svc.SetOnHandAsync(v1, 5m);
        await svc.SetOnHandAsync(v2, 1m);

        var act = async () => await svc.ReserveAsync(Guid.NewGuid(), new[]
        {
            new InventoryReservationLine(v1, 2m),
            new InventoryReservationLine(v2, 3m),
        });

        await act.Should().ThrowAsync<InsufficientStockException>();
        // v1 must be untouched — nothing was reserved.
        (await svc.GetAvailableAsync(v1)).Should().Be(5m);
    }

    [Fact]
    public async Task Commit_Should_DrawDownOnHand_AndClearReserved()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        var cart = Guid.NewGuid();
        await svc.SetOnHandAsync(variant, 10m);
        await svc.ReserveAsync(cart, new[] { new InventoryReservationLine(variant, 4m) });

        await svc.CommitAsync(cart);

        // OnHand 10 -> 6, Reserved -> 0, so Available = 6.
        (await svc.GetAvailableAsync(variant)).Should().Be(6m);
    }

    [Fact]
    public async Task Release_Should_FreeReservedStock()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        var cart = Guid.NewGuid();
        await svc.SetOnHandAsync(variant, 10m);
        await svc.ReserveAsync(cart, new[] { new InventoryReservationLine(variant, 4m) });

        await svc.ReleaseAsync(cart);

        (await svc.GetAvailableAsync(variant)).Should().Be(10m);
    }

    [Fact]
    public async Task ReleaseExpired_Should_ReleaseOnlyExpiredHolds()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, clock, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        var cart = Guid.NewGuid();
        await svc.SetOnHandAsync(variant, 10m);
        await svc.ReserveAsync(cart, new[] { new InventoryReservationLine(variant, 4m) }); // expires at now + 30m

        // Sweep before expiry — nothing released.
        (await svc.ReleaseExpiredAsync(clock.UtcNow.AddMinutes(10))).Should().Be(0);
        (await svc.GetAvailableAsync(variant)).Should().Be(6m);

        // Sweep after expiry — released, stock freed.
        (await svc.ReleaseExpiredAsync(clock.UtcNow.AddMinutes(31))).Should().Be(1);
        (await svc.GetAvailableAsync(variant)).Should().Be(10m);
    }
}
