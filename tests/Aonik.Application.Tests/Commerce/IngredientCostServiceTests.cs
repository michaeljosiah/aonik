using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Sourcing;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Effective-dated ingredient costs (Spec 051 §8, R1–R4): set/get-current roundtrip, the
/// close-prior/open-new repricing transition with preserved history, the WINDOW-SPLIT transition
/// (a cost landing before a scheduled row splits the containing window instead of being locked
/// out — only fully-elapsed windows are immutable), DATE-AWARE current-cost resolution (a
/// future-dated cost is a scheduled row that does not price "now"), and the single-open-row
/// invariant. InMemory cannot enforce the filtered unique index — these tests prove the service
/// transition; the index is the SQL Server concurrency backstop.
/// </summary>
public class IngredientCostServiceTests
{
    private static (IngredientCostService Service, CommerceDbContext Ctx, CommerceTestHarness.TestClock Clock) Build(
        DbContextOptions<CommerceDbContext> options, Guid tenantId)
    {
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var clock = new CommerceTestHarness.TestClock();
        var svc = new IngredientCostService(ctx, new TestTenantProvider(tenantId), clock);
        return (svc, ctx, clock);
    }

    private static async Task<Ingredient> SeedIngredientAsync(
        CommerceDbContext ctx, Guid tenantId, string name = "Rice",
        string baseUnit = IngredientBaseUnits.Kg, bool isActive = true)
    {
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            BaseUnit = baseUnit,
            IsActive = isActive,
        };
        ctx.Ingredients.Add(ingredient);
        await ctx.SaveChangesAsync();
        return ingredient;
    }

    [Fact]
    public async Task SetCost_Then_GetCurrentCost_Should_Roundtrip()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        var set = await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));

        set.IngredientId.Should().Be(rice.Id);
        set.Currency.Should().Be("NGN");
        set.UnitCost.Should().Be(1_200m);
        set.EffectiveFrom.Should().Be(clock.UtcNow);
        set.EffectiveTo.Should().BeNull();
        set.IsActive.Should().BeTrue();

        var current = await svc.GetCurrentCostAsync(rice.Id, "NGN");
        current.Should().NotBeNull();
        current!.Id.Should().Be(set.Id);
        current.UnitCost.Should().Be(1_200m);

        // Currency is normalized — a lower-case query resolves the same cost.
        (await svc.GetCurrentCostAsync(rice.Id, "ngn"))!.UnitCost.Should().Be(1_200m);
    }

    [Fact]
    public async Task SetCost_Should_ClosePriorRow_AndPreserveHistory_WhenRepricing()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        var first = await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_100m));

        clock.UtcNow = clock.UtcNow.AddDays(7);
        var repriceAt = clock.UtcNow;
        var second = await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));

        // The new cost is current; the prior row survives, closed at the new EffectiveFrom (R2).
        (await svc.GetCurrentCostAsync(rice.Id, "NGN"))!.UnitCost.Should().Be(1_200m);

        var history = await svc.ListHistoryAsync(rice.Id, "NGN");
        history.Should().HaveCount(2);
        history[0].Id.Should().Be(second.Id);          // newest first
        history[1].Id.Should().Be(first.Id);
        history[1].UnitCost.Should().Be(1_100m);       // value never overwritten
        history[1].EffectiveTo.Should().Be(repriceAt); // prior EffectiveTo = new EffectiveFrom
        history[1].IsActive.Should().BeFalse();
        history[0].EffectiveTo.Should().BeNull();

        // Point-in-time: "what did rice cost before the reprice?" resolves the closed row (R3).
        var before = await svc.GetCurrentCostAsync(rice.Id, "NGN", repriceAt.AddDays(-1));
        before!.UnitCost.Should().Be(1_100m);
    }

    [Fact]
    public async Task GetCurrentCost_Should_ReturnPriorCost_WhenNewCostIsFutureDated_UntilBoundaryPasses()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));

        // Schedule a reprice for tomorrow (R4): stored immediately, but not current yet.
        var tomorrow = clock.UtcNow.AddDays(1);
        var scheduled = await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_350m, tomorrow));
        scheduled.EffectiveFrom.Should().Be(tomorrow);
        scheduled.EffectiveTo.Should().BeNull();

        // DATE-AWARE resolution: "now" still returns the old cost — IsActive is not the selector.
        (await svc.GetCurrentCostAsync(rice.Id, "NGN"))!.UnitCost.Should().Be(1_200m);

        // Asking at/after the boundary returns the scheduled cost ([EffectiveFrom, ∞) half-open).
        (await svc.GetCurrentCostAsync(rice.Id, "NGN", tomorrow))!.UnitCost.Should().Be(1_350m);

        // Advance the clock past the boundary — "now" flips to the new cost with no further write.
        clock.UtcNow = tomorrow.AddHours(1);
        (await svc.GetCurrentCostAsync(rice.Id, "NGN"))!.UnitCost.Should().Be(1_350m);
    }

    [Fact]
    public async Task SetCost_Should_KeepExactlyOneOpenRow_AfterMultipleReprices()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_000m));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_100m));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));

        // The service invariant behind the DB filtered unique index (R3): one open row only.
        await using var verify = CommerceTestHarness.CreateContext(options, tenantId);
        var rows = await verify.IngredientCosts
            .Where(c => c.IngredientId == rice.Id && c.Currency == "NGN")
            .ToListAsync();
        rows.Should().HaveCount(3);
        var open = rows.Where(c => c.EffectiveTo == null).ToList();
        open.Should().ContainSingle().Which.UnitCost.Should().Be(1_200m);
    }

    [Fact]
    public async Task GetCurrentCost_Should_ReturnNull_WhenNoCostIsEffective()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        // No cost at all.
        (await svc.GetCurrentCostAsync(rice.Id, "NGN")).Should().BeNull();

        // A first cost scheduled for the future is not effective "now" (R4).
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 900m, clock.UtcNow.AddDays(3)));
        (await svc.GetCurrentCostAsync(rice.Id, "NGN")).Should().BeNull();

        // A cost in another currency does not satisfy an NGN query — no FX conversion (R7).
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "USD", 1.5m));
        (await svc.GetCurrentCostAsync(rice.Id, "GBP")).Should().BeNull();
    }

    [Fact]
    public async Task ListHistory_Should_ReturnNewestFirst_AndFilterByCurrency()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_000m));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_100m));
        clock.UtcNow = clock.UtcNow.AddDays(1);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "USD", 0.8m));

        var all = await svc.ListHistoryAsync(rice.Id);
        all.Should().HaveCount(3);
        all.Select(c => c.UnitCost).Should().ContainInOrder(0.8m, 1_100m, 1_000m);

        var ngnOnly = await svc.ListHistoryAsync(rice.Id, "NGN");
        ngnOnly.Should().HaveCount(2);
        ngnOnly.Select(c => c.UnitCost).Should().ContainInOrder(1_100m, 1_000m);
    }

    [Fact]
    public async Task SetCost_Should_Throw_WhenUnitCostNotPositive()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, _) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        var act = async () => await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 0m));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*must be positive*");
    }

    [Fact]
    public async Task SetCost_Should_Throw_WhenCurrencyMissing()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, _) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        var act = async () => await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, " ", 1_000m));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Currency is required*");
    }

    [Fact]
    public async Task SetCost_Should_Throw_WhenIngredientUnknown()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, _) = Build(options, tenantId);
        await using var _ctx = ctx;

        var act = async () => await svc.SetCostAsync(new SetIngredientCostCommand(Guid.NewGuid(), "NGN", 1_000m));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");
    }

    [Fact]
    public async Task SetCost_Should_Throw_WhenIngredientInactive()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, _) = Build(options, tenantId);
        await using var _ctx = ctx;

        var oldStock = await SeedIngredientAsync(ctx, tenantId, "Old stock", isActive: false);

        var act = async () => await svc.SetCostAsync(new SetIngredientCostCommand(oldStock.Id, "NGN", 1_000m));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*deactivated*");
    }

    [Fact]
    public async Task SetCost_Should_Throw_WhenBackdatedBeforeAllRecordedHistory()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_000m));

        // No window contains a date before the first row — there is nothing to split, and pricing
        // time before the recorded history would rewrite it (§8) — rejected.
        var act = async () => await svc.SetCostAsync(
            new SetIngredientCostCommand(rice.Id, "NGN", 900m, clock.UtcNow.AddDays(-1)));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no cost window*contains that date*");
    }

    [Fact]
    public async Task SetCost_Should_SplitCurrentWindow_WhenRepricingTodayWhileFutureCostIsScheduled()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        var initialAt = clock.UtcNow;
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m));

        // Schedule a reprice for tomorrow — the scheduled row becomes the single open row (R4).
        var tomorrow = initialAt.AddDays(1);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_350m, tomorrow));

        // Two hours later the maker corrects TODAY's cost. The scheduled row must not lock this
        // out: the currently-effective window is split at the correction date (§8), and the
        // correction is inserted CLOSED at the scheduled boundary.
        clock.UtcNow = initialAt.AddHours(2);
        var correctionAt = clock.UtcNow;
        var correction = await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_250m));
        correction.EffectiveFrom.Should().Be(correctionAt);
        correction.EffectiveTo.Should().Be(tomorrow);

        // Date-aware resolution: the correction prices "now"; the scheduled cost still takes over
        // at the boundary.
        (await svc.GetCurrentCostAsync(rice.Id, "NGN"))!.UnitCost.Should().Be(1_250m);
        (await svc.GetCurrentCostAsync(rice.Id, "NGN", tomorrow))!.UnitCost.Should().Be(1_350m);

        // Exactly one open row remains — the scheduled one — and the windows are contiguous.
        await using var verify = CommerceTestHarness.CreateContext(options, tenantId);
        var rows = await verify.IngredientCosts
            .Where(c => c.IngredientId == rice.Id && c.Currency == "NGN")
            .OrderBy(c => c.EffectiveFrom)
            .ToListAsync();
        rows.Should().HaveCount(3);
        rows.Where(c => c.EffectiveTo == null).Should().ContainSingle().Which.UnitCost.Should().Be(1_350m);
        rows[0].EffectiveTo.Should().Be(rows[1].EffectiveFrom);
        rows[1].EffectiveTo.Should().Be(rows[2].EffectiveFrom);
    }

    [Fact]
    public async Task SetCost_Should_SplitScheduledWindow_WhenInsertedBetweenTwoScheduledCosts()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);

        var day0 = clock.UtcNow;
        var day7 = day0.AddDays(7);
        var day10 = day0.AddDays(10);
        var day14 = day0.AddDays(14);

        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_000m));
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_100m, day7));
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_200m, day14));

        // Insert between the two scheduled costs: the [day7, day14) window is split at day10; the
        // day14 row is preserved and stays the single open row.
        var inserted = await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_150m, day10));
        inserted.EffectiveTo.Should().Be(day14);

        // Correct resolution at each boundary.
        (await svc.GetCurrentCostAsync(rice.Id, "NGN", day0))!.UnitCost.Should().Be(1_000m);
        (await svc.GetCurrentCostAsync(rice.Id, "NGN", day7))!.UnitCost.Should().Be(1_100m);
        (await svc.GetCurrentCostAsync(rice.Id, "NGN", day10))!.UnitCost.Should().Be(1_150m);
        (await svc.GetCurrentCostAsync(rice.Id, "NGN", day14))!.UnitCost.Should().Be(1_200m);

        // Windows stay contiguous with exactly one open row (the last scheduled cost).
        await using var verify = CommerceTestHarness.CreateContext(options, tenantId);
        var rows = await verify.IngredientCosts
            .Where(c => c.IngredientId == rice.Id && c.Currency == "NGN")
            .OrderBy(c => c.EffectiveFrom)
            .ToListAsync();
        rows.Should().HaveCount(4);
        for (var i = 0; i < rows.Count - 1; i++)
        {
            rows[i].EffectiveTo.Should().Be(rows[i + 1].EffectiveFrom);
        }
        rows[^1].EffectiveTo.Should().BeNull();
        rows[^1].UnitCost.Should().Be(1_200m);
    }

    [Fact]
    public async Task SetCost_Should_Throw_WhenEffectiveFromFallsInFullyElapsedWindow()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx, clock) = Build(options, tenantId);
        await using var _ctx = ctx;

        var rice = await SeedIngredientAsync(ctx, tenantId);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_000m));

        clock.UtcNow = clock.UtcNow.AddDays(7);
        await svc.SetCostAsync(new SetIngredientCostCommand(rice.Id, "NGN", 1_100m));

        // The first window has fully elapsed — it already priced the past. An effectiveFrom before
        // the currently-effective window's start stays forbidden (§8): history is immutable.
        var act = async () => await svc.SetCostAsync(
            new SetIngredientCostCommand(rice.Id, "NGN", 900m, clock.UtcNow.AddDays(-1)));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already priced the past*");
    }
}
