using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Sourcing;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// The low-stock scan + alert lifecycle (Spec 052 §9/§10): raise at/below the reorder point,
/// refresh — never duplicate, never re-open an acknowledged alert, never auto-resolve — and
/// notify (via the outbox) on NEW alerts only.
/// </summary>
public class LowStockAlertServiceTests
{
    private static (LowStockAlertService Alerts, InventoryService Inventory, CommerceTestHarness.TestClock Clock, Guid Tenant, CommerceDbContext Ctx) Build(
        DbContextOptions<CommerceDbContext> options, Guid tenantId)
    {
        var clock = new CommerceTestHarness.TestClock();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var provider = new TestTenantProvider(tenantId);
        var tenantContext = new TenantContext { TenantId = tenantId };
        var alerts = new LowStockAlertService(ctx, provider, tenantContext, clock);
        var inventory = new InventoryService(ctx, provider, tenantContext, clock);
        return (alerts, inventory, clock, tenantId, ctx);
    }

    private static async Task<Guid> SeedIngredientAsync(CommerceDbContext ctx, Guid tenantId, string name = "Rice")
    {
        var id = Guid.NewGuid();
        ctx.Ingredients.Add(new Ingredient
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

    private static Task<List<OutboxMessage>> RaisedEventsAsync(CommerceDbContext ctx)
        => ctx.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType == typeof(LowStockAlertRaisedEvent).FullName)
            .ToListAsync();

    [Fact]
    public async Task Scan_Should_RaiseOpenAlert_AndEnqueueEventOnce_WhenAvailableAtOrBelowReorderPoint()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        // Reserved counts against availability: OnHand 8 - Reserved 6 = 2 <= 5.
        await inventory.SetOnHandAsync(rice, 8m);
        await inventory.ReserveAsync(Guid.NewGuid(), new[] { new InventoryReservationLine(rice, 6m) });
        await inventory.SetReorderPointAsync(rice, 5m, 25m);

        var result = await alerts.ScanAndRaiseAsync();

        result.Should().Be(new Aonik.Commerce.Contracts.Models.Sourcing.LowStockScanResult(1, 0));
        var alert = await ctx.LowStockAlerts.SingleAsync();
        alert.IngredientId.Should().Be(rice.Id);
        alert.Status.Should().Be(LowStockAlertStatuses.Open);
        alert.AvailableAtRaise.Should().Be(2m);
        alert.ReorderPoint.Should().Be(5m);

        // Exactly one LowStockAlertRaisedEvent staged in the same transaction, tenant-stamped.
        var events = await RaisedEventsAsync(ctx);
        events.Should().HaveCount(1);
        events[0].TenantId.Should().Be(tenantId);
        events[0].Payload.Should().Contain("Rice").And.Contain(alert.Id.ToString());
    }

    [Fact]
    public async Task Scan_Should_RefreshOpenAlert_NotDuplicate_AndNotReEnqueue_OnSecondScan()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        await alerts.ScanAndRaiseAsync();

        // Stock moves further down; the second scan refreshes the snapshot silently.
        await inventory.SetOnHandAsync(rice, 1m);
        var second = await alerts.ScanAndRaiseAsync();

        second.Raised.Should().Be(0);
        second.Refreshed.Should().Be(1);
        var alert = await ctx.LowStockAlerts.SingleAsync();   // still exactly ONE alert
        alert.Status.Should().Be(LowStockAlertStatuses.Open);
        alert.AvailableAtRaise.Should().Be(1m);               // snapshot refreshed
        (await RaisedEventsAsync(ctx)).Should().HaveCount(1); // event NOT re-enqueued
    }

    [Fact]
    public async Task Scan_Should_RefreshAcknowledgedAlert_WithoutReopeningIt()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        await alerts.ScanAndRaiseAsync();
        var alertId = (await ctx.LowStockAlerts.SingleAsync()).Id;
        await alerts.AcknowledgeAsync(alertId);

        // Still below the reorder point — the acknowledged alert stays the single ACTIVE alert.
        var rescan = await alerts.ScanAndRaiseAsync();

        rescan.Raised.Should().Be(0);
        rescan.Refreshed.Should().Be(1);
        var alert = await ctx.LowStockAlerts.SingleAsync();
        alert.Id.Should().Be(alertId);
        alert.Status.Should().Be(LowStockAlertStatuses.Acknowledged); // NOT flipped back to Open
        (await RaisedEventsAsync(ctx)).Should().HaveCount(1);         // no re-notification
    }

    [Fact]
    public async Task Scan_Should_IgnoreIngredientsAboveThreshold_OrWithoutReorderPoint_AndVariantLevels()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        // Above threshold — not alerted.
        var beans = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId, "Beans"));
        await inventory.SetOnHandAsync(beans, 50m);
        await inventory.SetReorderPointAsync(beans, 5m);

        // No reorder point — never scanned (Spec 052 §9).
        var salt = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId, "Salt"));
        await inventory.SetOnHandAsync(salt, 0m);

        // Variant level at zero — the scan never reads variant levels.
        await inventory.SetOnHandAsync(Guid.NewGuid(), 0m);

        var result = await alerts.ScanAndRaiseAsync();

        result.Raised.Should().Be(0);
        result.Refreshed.Should().Be(0);
        (await ctx.LowStockAlerts.AnyAsync()).Should().BeFalse();
        (await RaisedEventsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Scan_Should_NotAlert_WhenLevelIsSoftDeleted()
    {
        // The scan reads AcrossTenants(), which drops the soft-delete filter too — the service
        // must exclude deleted levels explicitly or a removed level keeps alerting admins.
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        var level = await ctx.InventoryLevels.SingleAsync(l => l.IngredientId == rice.Id);
        ctx.InventoryLevels.Remove(level); // soft delete (IsDeleted = true on save)
        await ctx.SaveChangesAsync();

        var result = await alerts.ScanAndRaiseAsync();

        result.Raised.Should().Be(0);
        result.Refreshed.Should().Be(0);
        (await ctx.LowStockAlerts.AnyAsync()).Should().BeFalse();
        (await RaisedEventsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Scan_Should_NotAlert_WhenIngredientIsSoftDeleted()
    {
        // Same bug class on the joined side: a soft-deleted ingredient with a still-breaching
        // live level must not raise or refresh anything.
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var riceId = await SeedIngredientAsync(ctx, tenantId);
        var rice = StockItemRef.Ingredient(riceId);
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        var ingredient = await ctx.Ingredients.SingleAsync(i => i.Id == riceId);
        ctx.Ingredients.Remove(ingredient); // soft delete (IsDeleted = true on save)
        await ctx.SaveChangesAsync();

        var result = await alerts.ScanAndRaiseAsync();

        result.Raised.Should().Be(0);
        result.Refreshed.Should().Be(0);
        (await ctx.LowStockAlerts.AnyAsync()).Should().BeFalse();
        (await RaisedEventsAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Scan_Should_NotAutoResolve_WhenStockClimbsBackAboveThreshold()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        await alerts.ScanAndRaiseAsync();

        // Incidental restock: resolution is a procurement event (Spec 054), not a stock wobble.
        await inventory.SetOnHandAsync(rice, 50m);
        var rescan = await alerts.ScanAndRaiseAsync();

        rescan.Raised.Should().Be(0);
        rescan.Refreshed.Should().Be(0);
        var alert = await ctx.LowStockAlerts.SingleAsync();   // the active alert persists untouched
        alert.Status.Should().Be(LowStockAlertStatuses.Open);
        alert.AvailableAtRaise.Should().Be(2m);
    }

    [Fact]
    public async Task Scan_Should_KeepOneActiveAlert_WhenTwoLocationsOfTheSameIngredientBreach()
    {
        // InMemory cannot enforce the filtered unique index — this proves the SERVICE invariant:
        // multiple breaching levels of one ingredient in a single pass yield one alert.
        var (alerts, inventory, clock, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var riceId = await SeedIngredientAsync(ctx, tenantId);
        var rice = StockItemRef.Ingredient(riceId);
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        // A second breaching level at a named location for the SAME ingredient.
        ctx.InventoryLevels.Add(new InventoryLevel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IngredientId = riceId,
            StockItemKind = StockItemKinds.Ingredient,
            Location = "cold-store",
            OnHand = 1m,
            Reserved = 0m,
            ReorderPoint = 5m,
        });
        await ctx.SaveChangesAsync();

        var result = await alerts.ScanAndRaiseAsync();

        result.Raised.Should().Be(1);
        result.Refreshed.Should().Be(1);
        (await ctx.LowStockAlerts.CountAsync()).Should().Be(1);
        (await RaisedEventsAsync(ctx)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Scan_Should_SweepAcrossTenants_WithoutAmbientTenant()
    {
        // Mirrors the Worker: the scan runs with no ambient tenant over a context-backed provider,
        // setting the tenant per group so EnforceTenantOnWrites passes and outbox rows are stamped.
        var dbName = $"co_lowstock_{Guid.NewGuid()}";
        var shared = new TenantContext();
        var clock = new CommerceTestHarness.TestClock();
        var provider = new HttpContextTenantProvider(shared);
        CommerceDbContext Ctx() => new(
            new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(dbName).Options,
            provider, new TestCurrentUserProvider());
        InventoryService Inv() => new(Ctx(), provider, shared, clock);
        LowStockAlertService Alerts() => new(Ctx(), provider, shared, clock);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();

        shared.TenantId = t1;
        await using (var seedCtx = Ctx())
        {
            var rice = StockItemRef.Ingredient(await SeedIngredientAsync(seedCtx, t1, "Rice"));
            await Inv().SetOnHandAsync(rice, 2m);
            await Inv().SetReorderPointAsync(rice, 5m);
        }

        shared.TenantId = t2;
        await using (var seedCtx = Ctx())
        {
            var oil = StockItemRef.Ingredient(await SeedIngredientAsync(seedCtx, t2, "Oil"));
            await Inv().SetOnHandAsync(oil, 1m);
            await Inv().SetReorderPointAsync(oil, 3m);
        }

        // Worker scan — no ambient tenant.
        shared.TenantId = null;
        var result = await Alerts().ScanAndRaiseAsync();

        result.Raised.Should().Be(2);
        shared.IsResolved.Should().BeFalse(); // ambient restored to null afterwards

        await using var verify = Ctx();
        shared.TenantId = t1;
        (await verify.LowStockAlerts.CountAsync(a => a.TenantId == t1)).Should().Be(1);
        shared.TenantId = t2;
        (await verify.LowStockAlerts.CountAsync(a => a.TenantId == t2)).Should().Be(1);
    }

    [Fact]
    public async Task Acknowledge_Should_TransitionOpenToAcknowledged_AndBeIdempotent()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        await alerts.ScanAndRaiseAsync();
        var alertId = (await ctx.LowStockAlerts.SingleAsync()).Id;

        var acknowledged = await alerts.AcknowledgeAsync(alertId);
        acknowledged!.Status.Should().Be(LowStockAlertStatuses.Acknowledged);
        acknowledged.IngredientName.Should().Be("Rice");

        // Acknowledging again is a no-op, not an error.
        (await alerts.AcknowledgeAsync(alertId))!.Status.Should().Be(LowStockAlertStatuses.Acknowledged);
    }

    [Fact]
    public async Task Acknowledge_Should_ReturnNull_WhenAlertUnknown()
    {
        var (alerts, _, _, _, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        (await alerts.AcknowledgeAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task Acknowledge_Should_Throw_WhenAlertHasLeftTheActiveSet()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        await alerts.ScanAndRaiseAsync();
        var alert = await ctx.LowStockAlerts.SingleAsync();
        alert.Status = LowStockAlertStatuses.Ordered; // Spec 053 will own this transition
        await ctx.SaveChangesAsync();

        var act = async () => await alerts.AcknowledgeAsync(alert.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task List_Should_FilterByStatus_AndCarryIngredientName()
    {
        var (alerts, inventory, _, tenantId, ctx) = Build(CommerceTestHarness.NewDb().Options, Guid.NewGuid());
        await using var _ctx = ctx;

        var rice = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId, "Rice"));
        var oil = StockItemRef.Ingredient(await SeedIngredientAsync(ctx, tenantId, "Oil"));
        await inventory.SetOnHandAsync(rice, 2m);
        await inventory.SetReorderPointAsync(rice, 5m);
        await inventory.SetOnHandAsync(oil, 1m);
        await inventory.SetReorderPointAsync(oil, 3m);
        await alerts.ScanAndRaiseAsync();
        var riceAlertId = (await ctx.LowStockAlerts.SingleAsync(a => a.IngredientId == rice.Id)).Id;
        await alerts.AcknowledgeAsync(riceAlertId);

        var all = await alerts.ListAsync();
        all.Should().HaveCount(2);
        all.Select(a => a.IngredientName).Should().BeEquivalentTo("Rice", "Oil");

        var open = await alerts.ListAsync(LowStockAlertStatuses.Open);
        open.Should().ContainSingle().Which.IngredientId.Should().Be(oil.Id);

        var acknowledged = await alerts.ListAsync(LowStockAlertStatuses.Acknowledged);
        acknowledged.Should().ContainSingle().Which.IngredientId.Should().Be(rice.Id);
    }
}
