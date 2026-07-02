using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Inventory reservation lifecycle (Spec 042 §10): reserve, commit, release, expiry — generalized
/// by Spec 052 §8 so one engine holds variants and ingredients.
/// </summary>
public class InventoryServiceTests
{
    private static (InventoryService Service, CommerceTestHarness.TestClock Clock, Guid Tenant, Aonik.Commerce.Persistence.CommerceDbContext Ctx) Build(
        Microsoft.EntityFrameworkCore.DbContextOptions<Aonik.Commerce.Persistence.CommerceDbContext> options, Guid tenantId)
    {
        var clock = new CommerceTestHarness.TestClock();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var svc = new InventoryService(ctx, new TestTenantProvider(tenantId), new TenantContext { TenantId = tenantId }, clock);
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

        // A soft-deleted expired hold is invisible to the sweep — the sweep reads AcrossTenants(),
        // which drops the soft-delete filter too, so the service must exclude deleted rows.
        var deletedCart = Guid.NewGuid();
        await svc.ReserveAsync(deletedCart, new[] { new InventoryReservationLine(variant, 2m) });
        var deletedHold = await ctx.InventoryReservations.SingleAsync(r => r.HoldRef == deletedCart);
        ctx.InventoryReservations.Remove(deletedHold); // soft delete (IsDeleted = true on save)
        await ctx.SaveChangesAsync();

        (await svc.ReleaseExpiredAsync(clock.UtcNow.AddMinutes(31))).Should().Be(0);
        (await svc.GetAvailableAsync(variant)).Should().Be(8m); // its Reserved stays untouched
        var untouched = await ctx.InventoryReservations.IncludeSoftDeleted()
            .SingleAsync(r => r.HoldRef == deletedCart);
        untouched.Status.Should().Be(InventoryReservationStatuses.Held); // not flipped to Released
    }

    [Fact]
    public async Task ReleaseExpired_Should_SweepAcrossTenants_WithoutAmbientTenant()
    {
        // Mirrors the Worker: the sweep runs with no ambient tenant over a context-backed provider.
        // ReleaseExpiredAsync must set the tenant per group so EnforceTenantOnWrites passes.
        var dbName = $"co_sweep_{Guid.NewGuid()}";
        var shared = new TenantContext();
        var clock = new CommerceTestHarness.TestClock();
        var provider = new HttpContextTenantProvider(shared);
        CommerceDbContext Ctx() => new(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(dbName).Options,
            provider, new TestCurrentUserProvider());
        InventoryService Svc() => new(Ctx(), provider, shared, clock);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();

        shared.TenantId = t1;
        await Svc().SetOnHandAsync(v1, 10m);
        await Svc().ReserveAsync(Guid.NewGuid(), new[] { new InventoryReservationLine(v1, 4m) });

        shared.TenantId = t2;
        await Svc().SetOnHandAsync(v2, 10m);
        await Svc().ReserveAsync(Guid.NewGuid(), new[] { new InventoryReservationLine(v2, 5m) });

        // Worker sweep — no ambient tenant.
        shared.TenantId = null;
        var released = await Svc().ReleaseExpiredAsync(clock.UtcNow.AddMinutes(31));
        released.Should().Be(2);

        // Both tenants' stock is freed, and the ambient tenant is restored to null afterwards.
        shared.IsResolved.Should().BeFalse();
        shared.TenantId = t1;
        (await Svc().GetAvailableAsync(v1)).Should().Be(10m);
        shared.TenantId = t2;
        (await Svc().GetAvailableAsync(v2)).Should().Be(10m);
    }

    // ── Spec 052 §8 — the same engine keyed by stock item (ingredients) ─────────────────────────

    private static async Task<Guid> SeedIngredientAsync(CommerceDbContext ctx, Guid tenantId, string name = "Rice")
    {
        var id = Guid.NewGuid();
        ctx.Ingredients.Add(new Aonik.Commerce.Entities.Sourcing.Ingredient
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            BaseUnit = IngredientBaseUnits.Kg,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task SetOnHand_Then_GetAvailable_Should_Work_ForIngredient()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await svc.SetOnHandAsync(rice, 25m);

        (await svc.GetAvailableAsync(rice)).Should().Be(25m);

        // The level row carries the ingredient identity, never a variant's (exactly-one invariant).
        var level = await ctx.InventoryLevels.SingleAsync(l => l.IngredientId == rice.Id);
        level.StockItemKind.Should().Be(StockItemKinds.Ingredient);
        level.ProductVariantId.Should().BeNull();
    }

    [Fact]
    public async Task SetOnHand_Should_Throw_WhenIngredientUnknown()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var act = async () => await svc.SetOnHandAsync(StockItemRef.Ingredient(Guid.NewGuid()), 5m);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");
    }

    [Fact]
    public async Task Reserve_Commit_Should_DrawDownIngredientStock_ThroughTheSameEngine()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        var hold = Guid.NewGuid();
        await svc.SetOnHandAsync(rice, 10m);

        await svc.ReserveAsync(hold, new[] { new InventoryReservationLine(rice, 4m) });
        (await svc.GetAvailableAsync(rice)).Should().Be(6m);

        // The hold row records WHICH stock item it holds (Spec 052 §8) — no parallel hold table.
        var reservation = await ctx.InventoryReservations.SingleAsync(r => r.HoldRef == hold);
        reservation.IngredientId.Should().Be(rice.Id);
        reservation.ProductVariantId.Should().BeNull();
        reservation.StockItemKind.Should().Be(StockItemKinds.Ingredient);

        await svc.CommitAsync(hold);

        // OnHand 10 -> 6, Reserved -> 0.
        (await svc.GetAvailableAsync(rice)).Should().Be(6m);
        var level = await ctx.InventoryLevels.SingleAsync(l => l.IngredientId == rice.Id);
        level.OnHand.Should().Be(6m);
        level.Reserved.Should().Be(0m);
    }

    [Fact]
    public async Task Release_Should_FreeIngredientHold()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        var hold = Guid.NewGuid();
        await svc.SetOnHandAsync(rice, 10m);
        await svc.ReserveAsync(hold, new[] { new InventoryReservationLine(rice, 4m) });

        await svc.ReleaseAsync(hold);

        (await svc.GetAvailableAsync(rice)).Should().Be(10m);
    }

    [Fact]
    public async Task Reserve_Should_BeAllOrNothing_AcrossMixedVariantAndIngredientLines()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await svc.SetOnHandAsync(variant, 5m);
        await svc.SetOnHandAsync(rice, 1m);

        var act = async () => await svc.ReserveAsync(Guid.NewGuid(), new[]
        {
            new InventoryReservationLine(variant, 2m),          // satisfiable
            new InventoryReservationLine(rice, 3m),             // not satisfiable
        });

        await act.Should().ThrowAsync<InsufficientStockException>();
        // Neither kind was touched — the hold is atomic across mixed lines.
        (await svc.GetAvailableAsync(variant)).Should().Be(5m);
        (await svc.GetAvailableAsync(rice)).Should().Be(1m);
    }

    [Fact]
    public async Task Reserve_Commit_Should_HandleMixedHold_WhenBothKindsAreSatisfiable()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var variant = Guid.NewGuid();
        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        var hold = Guid.NewGuid();
        await svc.SetOnHandAsync(variant, 5m);
        await svc.SetOnHandAsync(rice, 10m);

        await svc.ReserveAsync(hold, new[]
        {
            new InventoryReservationLine(variant, 2m),
            new InventoryReservationLine(rice, 4m),
        });
        await svc.CommitAsync(hold);

        (await svc.GetAvailableAsync(variant)).Should().Be(3m);
        (await svc.GetAvailableAsync(rice)).Should().Be(6m);
    }

    [Fact]
    public async Task ReleaseExpired_Should_ReleaseExpiredIngredientHolds()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, clock, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await svc.SetOnHandAsync(rice, 10m);
        await svc.ReserveAsync(Guid.NewGuid(), new[] { new InventoryReservationLine(rice, 4m) }); // expires at now + 30m

        // Before expiry — the ingredient hold survives the sweep.
        (await svc.ReleaseExpiredAsync(clock.UtcNow.AddMinutes(10))).Should().Be(0);
        (await svc.GetAvailableAsync(rice)).Should().Be(6m);

        // After expiry — the SAME sweep frees ingredient stock (no second sweep job).
        (await svc.ReleaseExpiredAsync(clock.UtcNow.AddMinutes(31))).Should().Be(1);
        (await svc.GetAvailableAsync(rice)).Should().Be(10m);
    }

    [Fact]
    public async Task SetReorderPoint_Should_PersistThresholds_AndClear()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await svc.SetOnHandAsync(rice, 10m);

        var level = await svc.SetReorderPointAsync(rice, 5m, 25m);
        level.ReorderPoint.Should().Be(5m);
        level.ReorderQuantity.Should().Be(25m);
        level.Available.Should().Be(10m);

        // Null clears alerting (Spec 052 §9).
        var cleared = await svc.SetReorderPointAsync(rice, null, null);
        cleared.ReorderPoint.Should().BeNull();
        cleared.ReorderQuantity.Should().BeNull();
    }

    [Fact]
    public async Task SetReorderPoint_Should_RejectNegativeThreshold()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, _, _, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));

        var act = async () => await svc.SetReorderPointAsync(rice, -1m);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
