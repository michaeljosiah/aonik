using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Production;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Production;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Ordering.Persistence;
using Aonik.Ordering.Services;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Production / work orders + the kitchen sheet (Spec 056), over the REAL composed services
/// (050 RecipeService, 052 InventoryService, 055 ProductionPlanningService + the spine's
/// CoreOrderService for the from-sheet seed — the GoodsReceiptServiceTests pattern). Proves the
/// §7 creation-time snapshot freeze (per-portion, from ExplodeAsync(variant, 1); no-recipe
/// variants rejected on the explicit path, skipped-and-reported on the from-sheet path), the
/// §9 release consumption (merged bill from the FROZEN snapshots — the #164 fix end-to-end: a
/// recipe edited after creation changes neither the kitchen sheet nor what release draws down —
/// all-or-nothing in ONE SaveChanges, availability counted as OnHand − Reserved, recompute-from-
/// scratch on an injected rowversion conflict), the §8 guards (single-shot consume; re-release
/// no-op; post-release cancel never restocks), the §10 completion yield (produced defaults to
/// planned; explicit actuals; the yield toggle), and the §11 kitchen sheet (numbers identical to
/// release consumption by construction).
/// </summary>
public class ProductionOrderServiceTests
{
    private static readonly DateTime PlannedFor = new(2026, 7, 6, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FromUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);

    // ── harness — real services over ONE shared CommerceDbContext ───────────────────────────────

    private sealed class Harness
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _commerceDb = $"po56_c_{Guid.NewGuid()}";
        private readonly string _orderingDb = $"po56_o_{Guid.NewGuid()}";
        private readonly TestTenantProvider _tenant;
        private readonly TestCurrentUserProvider _user = new();
        private readonly ConcurrencyConflictInterceptor? _interceptor;
        private readonly CommerceDbContext _sharedCommerce;

        /// <summary>
        /// ONE CommerceDbContext for the whole composed service graph, mirroring production DI:
        /// CommerceModule registers the context and every Commerce service AddScoped, so within a
        /// request scope ProductionOrderService, RecipeService, InventoryService, and the planning
        /// service all share the SAME instance. Release's one-SaveChanges all-or-nothing and
        /// completion's marker-style flip riding the first yield increment BOTH rely on that. Pass
        /// an interceptor to inject rowversion conflicts into the graph's saves (InMemory never
        /// raises them on its own).
        /// </summary>
        public Harness(ConcurrencyConflictInterceptor? interceptor = null)
        {
            _tenant = new TestTenantProvider(_tenantId);
            _interceptor = interceptor;
            _sharedCommerce = CommerceTestHarness.CreateContext(CommerceOptions(intercepted: true), _tenantId, Clock);
        }

        public CommerceTestHarness.TestClock Clock { get; } = new();

        private DbContextOptions<CommerceDbContext> CommerceOptions(bool intercepted = false)
        {
            var builder = new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(_commerceDb);
            if (intercepted && _interceptor is not null)
            {
                builder.AddInterceptors(_interceptor);
            }
            return builder.Options;
        }

        /// <summary>A FRESH, un-intercepted context over the same store — seeding/asserting outside
        /// the service graph, and standing in for a rival request scope in the conflict tests.</summary>
        public CommerceDbContext Commerce() => CommerceTestHarness.CreateContext(CommerceOptions(), _tenantId, Clock);

        public OrderingDbContext Ordering() => new(
            new DbContextOptionsBuilder<OrderingDbContext>().UseInMemoryDatabase(_orderingDb).Options,
            _tenant, _user, Clock);

        public CoreOrderService Orders() => new(Ordering(), _tenant, Clock, _user);

        public RecipeService Recipes() => new(_sharedCommerce, _tenant);

        public InventoryService Inventory() => new(_sharedCommerce, _tenant, new TenantContext { TenantId = _tenantId }, Clock);

        public ProductionPlanningService Planning() => new(_sharedCommerce, Orders(), Recipes(), Inventory(), _tenant);

        public ProductionOrderService ProductionOrders() => new(_sharedCommerce, Recipes(), Planning(), Inventory(), _tenant, Clock);

        public async Task<Guid> SeedVariantAsync(string productName, string variantName)
        {
            await using var ctx = Commerce();
            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Slug = $"{productName.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
                Name = productName,
                Kind = ProductKinds.Simple,
            };
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ProductId = product.Id,
                Sku = $"SKU-{Guid.NewGuid():N}",
                Name = variantName,
            };
            ctx.Products.Add(product);
            ctx.ProductVariants.Add(variant);
            await ctx.SaveChangesAsync();
            return variant.Id;
        }

        public async Task<Guid> SeedIngredientAsync(string name)
        {
            await using var ctx = Commerce();
            var id = Guid.NewGuid();
            ctx.Ingredients.Add(new Ingredient
            {
                Id = id,
                TenantId = _tenantId,
                Name = name,
                BaseUnit = IngredientBaseUnits.Kg,
                IsActive = true,
            });
            await ctx.SaveChangesAsync();
            return id;
        }

        public Task SetRecipeAsync(Guid variantId, string name, decimal yieldQuantity, params (Guid IngredientId, decimal Quantity)[] components)
            => Recipes().SetRecipeAsync(new SetRecipeCommand(
                variantId, name, yieldQuantity, "portion",
                components.Select(c => new RecipeComponentCommand(c.IngredientId, c.Quantity)).ToList()));

        public Task SetIngredientStockAsync(Guid ingredientId, decimal onHand)
            => Inventory().SetOnHandAsync(StockItemRef.Ingredient(ingredientId), onHand);

        public async Task<decimal> IngredientOnHandAsync(Guid ingredientId)
        {
            await using var ctx = Commerce();
            var level = await ctx.InventoryLevels.SingleOrDefaultAsync(l => l.IngredientId == ingredientId && l.Location == null);
            return level?.OnHand ?? 0m;
        }

        public async Task<decimal> VariantOnHandAsync(Guid variantId)
        {
            await using var ctx = Commerce();
            var level = await ctx.InventoryLevels.SingleOrDefaultAsync(l => l.ProductVariantId == variantId && l.Location == null);
            return level?.OnHand ?? 0m;
        }

        /// <summary>A committed ProductPurchase order on the spine — Spec 055 §9 demand for the
        /// from-sheet seed (checkout leaves Draft, which the sheet excludes; Pending counts).</summary>
        public async Task CreateDemandOrderAsync(DateTime createdAtUtc, params (Guid VariantId, decimal Quantity)[] lines)
        {
            Clock.UtcNow = createdAtUtc;
            var items = lines
                .Select((line, index) => new OrderItemCommand(
                    ItemType: OrderTypeCodes.ProductPurchase,
                    ItemIndex: index,
                    AmountIn: line.Quantity * 1_000m,
                    CurrencyIn: "NGN",
                    Quantity: line.Quantity,
                    UnitPrice: 1_000m,
                    ProductId: line.VariantId,
                    Sku: $"LINE-{index}"))
                .ToList();

            var order = await Orders().CreateAsync(new CreateOrderCommand(
                OrderType: OrderTypeCodes.ProductPurchase,
                PayerPartyId: Guid.NewGuid(),
                CurrencyIn: "NGN",
                Items: items));
            await Orders().TransitionAsync(order.Id, OrderStatusCodes.Pending, "test: committed demand");
        }

        /// <summary>Jollof: yield 4 portions from 1 kg rice + 0.5 kg tomato ⇒ 0.25 / 0.125 per portion
        /// (the Spec 056 §17 worked example).</summary>
        public async Task<(Guid VariantId, Guid RiceId, Guid TomatoId)> SeedJollofAsync()
        {
            var variantId = await SeedVariantAsync("Jollof Rice", "Regular");
            var riceId = await SeedIngredientAsync("Rice");
            var tomatoId = await SeedIngredientAsync("Tomato");
            await SetRecipeAsync(variantId, "Jollof (family pan)", 4m, (riceId, 1m), (tomatoId, 0.5m));
            return (variantId, riceId, tomatoId);
        }
    }

    // ── conflict injection (the landed 054 pattern): InMemory never raises rowversion conflicts
    // on its own, so the exact DbUpdateConcurrencyException SQL Server would produce is injected
    // via a SaveChanges interceptor while a rival commit lands through a separate context. ──

    private sealed class ConcurrencyConflictInterceptor : SaveChangesInterceptor
    {
        private int _remainingConflicts;
        private Func<Task>? _rivalWrite;

        public int SaveAttempts { get; private set; }

        public void Arm(int conflicts, Func<Task>? rivalWrite = null)
        {
            _remainingConflicts = conflicts;
            _rivalWrite = rivalWrite;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (_remainingConflicts > 0)
            {
                _remainingConflicts--;
                if (_rivalWrite is not null)
                {
                    await _rivalWrite(); // the rival's commit that SQL Server would have detected
                }
                throw new DbUpdateConcurrencyException("Simulated rowversion conflict.");
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    // ── §7 create: the snapshot freeze ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Should_FreezeThePerPortionSnapshotFromTheActiveRecipe()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }, Notes: "Sunday batch"));

        order.Status.Should().Be(ProductionOrderStatuses.Planned);
        order.PlannedFor.Should().Be(PlannedFor);
        order.Notes.Should().Be("Sunday batch");
        order.ReleasedAt.Should().BeNull();
        order.CompletedAt.Should().BeNull();

        var line = order.Lines.Single();
        line.ProductVariantId.Should().Be(variantId);
        line.PlannedQuantity.Should().Be(40m);
        line.ProducedQuantity.Should().BeNull();
        // The frozen per-portion bill = ExplodeAsync(variant, 1): recipe quantity / yield (050 §11).
        line.RecipeSnapshot.Should().BeEquivalentTo(new[]
        {
            new RecipeSnapshotComponent(riceId, "Rice", IngredientBaseUnits.Kg, 0.25m),
            new RecipeSnapshotComponent(tomatoId, "Tomato", IngredientBaseUnits.Kg, 0.125m),
        });

        // And the snapshot is PERSISTED on the line (§7/R9) — the durable record both the kitchen
        // sheet and release replay, not a projection recomputed per call.
        await using var ctx = h.Commerce();
        var persisted = await ctx.ProductionOrderLines.SingleAsync(l => l.ProductionOrderId == order.Id);
        JsonSerializer.Deserialize<List<RecipeSnapshotComponent>>(persisted.RecipeSnapshotJson)
            .Should().BeEquivalentTo(line.RecipeSnapshot);
    }

    [Fact]
    public async Task Create_Should_RejectAVariantWithoutAnActiveRecipe_NamingIt()
    {
        var h = new Harness();
        var steakId = await h.SeedVariantAsync("Grilled Steak", "300g"); // no recipe defined

        var act = async () => await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(steakId, 10m) }));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*No active recipe*300g*");

        await using var ctx = h.Commerce();
        (await ctx.ProductionOrders.CountAsync()).Should().Be(0); // nothing persisted
    }

    [Fact]
    public async Task Create_Should_RejectAnUnknownVariant_AndMergeDuplicateVariantLines()
    {
        var h = new Harness();
        var (variantId, _, _) = await h.SeedJollofAsync();

        var unknown = Guid.NewGuid();
        var act = async () => await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(unknown, 5m) }));
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*not found*{unknown}*");

        // Duplicate entries for one variant are merged — one dish per variant, quantities summed.
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor,
            new[] { new ProductionOrderLineCommand(variantId, 10m), new ProductionOrderLineCommand(variantId, 15m) }));
        var line = order.Lines.Single();
        line.PlannedQuantity.Should().Be(25m);
    }

    // ── §7 from-sheet seed (Spec 055 composition) ────────────────────────────────────────────────

    [Fact]
    public async Task CreateFromSheet_Should_SeedRecipeLines_AndReportSkippedVariants()
    {
        var h = new Harness();
        var (jollofId, _, _) = await h.SeedJollofAsync();
        var steakId = await h.SeedVariantAsync("Grilled Steak", "300g"); // demanded, but no recipe

        await h.CreateDemandOrderAsync(new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc), (jollofId, 8m), (steakId, 5m));
        await h.CreateDemandOrderAsync(new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc), (jollofId, 2m));

        var result = await h.ProductionOrders().CreateFromProductionSheetAsync(
            new CreateFromProductionSheetCommand(FromUtc, ToUtc));

        // The seed takes the sheet's aggregated demand for variants WITH a recipe…
        var line = result.Order.Lines.Single();
        line.ProductVariantId.Should().Be(jollofId);
        line.PlannedQuantity.Should().Be(10m); // 8 + 2 across the window's orders
        result.Order.Status.Should().Be(ProductionOrderStatuses.Planned);
        result.Order.PlannedFor.Should().Be(FromUtc); // defaults to the window start

        // …and REPORTS the no-recipe variants instead of silently dropping them (§7).
        result.SkippedVariants.Should().BeEquivalentTo(new[] { steakId });
    }

    [Fact]
    public async Task CreateFromSheet_Should_Throw_WhenNoDemandedVariantHasARecipe()
    {
        var h = new Harness();
        var steakId = await h.SeedVariantAsync("Grilled Steak", "300g"); // no recipe
        await h.CreateDemandOrderAsync(new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc), (steakId, 5m));

        var act = async () => await h.ProductionOrders().CreateFromProductionSheetAsync(
            new CreateFromProductionSheetCommand(FromUtc, ToUtc));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*active recipe*nothing to seed*");
    }

    // ── §11 kitchen sheet == §9 release consumption (the #164 fix, end-to-end) ───────────────────

    [Fact]
    public async Task KitchenSheet_Should_ReplayTheFrozenSnapshot_WhenTheRecipeIsEditedAfterCreation()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        // The kitchen has printed the sheet; NOW the recipe is edited (rice doubled).
        var sheetBefore = await h.ProductionOrders().GetKitchenSheetAsync(order.Id);
        await h.SetRecipeAsync(variantId, "Jollof (richer)", 4m, (riceId, 2m), (tomatoId, 0.5m));
        var sheetAfter = await h.ProductionOrders().GetKitchenSheetAsync(order.Id);

        // The sheet replays the FROZEN snapshot — identical before and after the edit (§7/§11).
        sheetAfter.Should().BeEquivalentTo(sheetBefore);

        var dish = sheetAfter!.Dishes.Single();
        dish.ProductName.Should().Be("Jollof Rice");
        dish.VariantName.Should().Be("Regular");
        dish.PlannedQuantity.Should().Be(40m);
        dish.Components.Should().BeEquivalentTo(new[]
        {
            new KitchenSheetComponentDto(riceId, "Rice", IngredientBaseUnits.Kg, 0.25m, 10m),
            new KitchenSheetComponentDto(tomatoId, "Tomato", IngredientBaseUnits.Kg, 0.125m, 5m),
        });
        sheetAfter.Totals.Should().BeEquivalentTo(new[]
        {
            new KitchenSheetTotalLineDto(riceId, "Rice", IngredientBaseUnits.Kg, 10m),
            new KitchenSheetTotalLineDto(tomatoId, "Tomato", IngredientBaseUnits.Kg, 5m),
        });
    }

    [Fact]
    public async Task Release_Should_ConsumeTheSnapshotBill_NotTheEditedRecipe_AndMatchTheKitchenSheet()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));
        var sheet = await h.ProductionOrders().GetKitchenSheetAsync(order.Id);

        // The recipe is edited AFTER the sheet went to the pass — the #164 divergence window.
        await h.SetRecipeAsync(variantId, "Jollof (richer)", 4m, (riceId, 2m), (tomatoId, 0.5m));

        h.Clock.UtcNow = new DateTime(2026, 7, 6, 6, 30, 0, DateTimeKind.Utc);
        var released = await h.ProductionOrders().ReleaseAsync(order.Id);

        released.Status.Should().Be(ProductionOrderStatuses.Released);
        released.ReleasedAt.Should().Be(h.Clock.UtcNow);

        // Consumption == the FROZEN snapshot (10 rice / 5 tomato), NOT the edited recipe (20 rice).
        (await h.IngredientOnHandAsync(riceId)).Should().Be(10m);
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(5m);

        // And the deltas are exactly the printed sheet's totals — sheet and release cannot diverge.
        sheet!.Totals.Single(t => t.IngredientId == riceId).RequiredQuantity.Should().Be(20m - 10m);
        sheet.Totals.Single(t => t.IngredientId == tomatoId).RequiredQuantity.Should().Be(10m - 5m);
    }

    // ── §9 release: merged fan-out, fail-fast, single-shot ──────────────────────────────────────

    [Fact]
    public async Task Release_Should_MergeSharedIngredientsAcrossLines_AndDecrementEachLevelOnce()
    {
        var h = new Harness();
        var (jollofId, riceId, tomatoId) = await h.SeedJollofAsync();
        var friedRiceId = await h.SeedVariantAsync("Fried Rice", "Regular");
        var oilId = await h.SeedIngredientAsync("Groundnut Oil");
        // Fried rice: yield 2 from 1 kg rice + 0.1 kg oil ⇒ 0.5 / 0.05 per portion.
        await h.SetRecipeAsync(friedRiceId, "Fried rice (pan)", 2m, (riceId, 1m), (oilId, 0.1m));

        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        await h.SetIngredientStockAsync(oilId, 2m);

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor,
            new[] { new ProductionOrderLineCommand(jollofId, 40m), new ProductionOrderLineCommand(friedRiceId, 10m) }));
        await h.ProductionOrders().ReleaseAsync(order.Id);

        // Rice is demanded by BOTH dishes: 40 × 0.25 + 10 × 0.5 = 15, drawn as ONE merged decrement.
        (await h.IngredientOnHandAsync(riceId)).Should().Be(5m);
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(5m);   // 10 − 40 × 0.125
        (await h.IngredientOnHandAsync(oilId)).Should().Be(1.5m);    // 2 − 10 × 0.05
    }

    [Fact]
    public async Task Release_Should_FailFastNamingTheIngredient_WithNothingConsumed_WhenAvailableIsShort()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        // Rice: 12 on hand but 4 RESERVED by a live checkout hold ⇒ available 8 < the 10 required.
        // Availability is OnHand − Reserved (§9) — raw on-hand would wrongly pass.
        await h.SetIngredientStockAsync(riceId, 12m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        await h.Inventory().ReserveAsync(Guid.NewGuid(), new[]
        {
            new InventoryReservationLine(StockItemRef.Ingredient(riceId), 4m),
        });

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        var act = async () => await h.ProductionOrders().ReleaseAsync(order.Id);
        var thrown = (await act.Should().ThrowAsync<InsufficientStockException>()).Which;
        thrown.StockItemId.Should().Be(riceId);
        thrown.Requested.Should().Be(10m);
        thrown.Available.Should().Be(8m);

        // ALL-OR-NOTHING: nothing was consumed — not even the satisfiable tomato line — and the
        // order still sits on the consume edge.
        (await h.IngredientOnHandAsync(riceId)).Should().Be(12m);
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(10m);
        await using var ctx = h.Commerce();
        var persisted = await ctx.ProductionOrders.SingleAsync(o => o.Id == order.Id);
        persisted.Status.Should().Be(ProductionOrderStatuses.Planned);
        persisted.ReleasedAt.Should().BeNull();
    }

    [Fact]
    public async Task Release_Should_Throw_WhenAnIngredientWasNeverStocked()
    {
        var h = new Harness();
        var (variantId, riceId, _) = await h.SeedJollofAsync();
        // No stock seeded at all: a missing level reads as zero available — never a silent create.

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        var act = async () => await h.ProductionOrders().ReleaseAsync(order.Id);
        var thrown = (await act.Should().ThrowAsync<InsufficientStockException>()).Which;
        thrown.StockItemId.Should().Be(riceId); // bill is name-ordered: Rice before Tomato
        thrown.Available.Should().Be(0m);

        await using var ctx = h.Commerce();
        (await ctx.InventoryLevels.CountAsync()).Should().Be(0); // no phantom zero-levels created
    }

    [Fact]
    public async Task Release_Should_BeANoOp_WhenAlreadyReleased_AndStockIsNeverDoubleConsumed()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        h.Clock.UtcNow = new DateTime(2026, 7, 6, 6, 30, 0, DateTimeKind.Utc);
        var first = await h.ProductionOrders().ReleaseAsync(order.Id);

        h.Clock.UtcNow = new DateTime(2026, 7, 6, 7, 0, 0, DateTimeKind.Utc);
        var second = await h.ProductionOrders().ReleaseAsync(order.Id);

        // Idempotent on the target edge (§8/R4): same state back, original release instant kept.
        second.Status.Should().Be(ProductionOrderStatuses.Released);
        second.ReleasedAt.Should().Be(first.ReleasedAt);
        (await h.IngredientOnHandAsync(riceId)).Should().Be(10m);   // consumed ONCE
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(5m);
    }

    [Fact]
    public async Task Release_Should_Throw_WhenTheOrderIsCancelledOrCompleted()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);

        var cancelled = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 4m) }));
        await h.ProductionOrders().CancelAsync(cancelled.Id, "menu change");
        var actCancelled = async () => await h.ProductionOrders().ReleaseAsync(cancelled.Id);
        (await actCancelled.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*is Cancelled*only a Planned*");

        var completed = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 4m) }));
        await h.ProductionOrders().ReleaseAsync(completed.Id);
        await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(completed.Id, YieldFinishedGoods: false));
        var actCompleted = async () => await h.ProductionOrders().ReleaseAsync(completed.Id);
        (await actCompleted.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*is Completed*only a Planned*");
    }

    // ── §9 release: recompute-from-scratch on a rowversion conflict ─────────────────────────────

    [Fact]
    public async Task Release_Should_RecomputeFromScratchAndSucceed_WhenARivalCommitConflictsTheSave()
    {
        var interceptor = new ConcurrencyConflictInterceptor();
        var h = new Harness(interceptor);
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 30m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        // First save conflicts AFTER a rival consumed 15 kg rice through a separate scope (the
        // shape SQL Server's rowversion token produces). The retry must DISCARD its staged
        // decrements + status flip, re-read fresh stock, re-validate, and re-apply from scratch —
        // a partial re-apply would draw rice twice.
        var attemptsBefore = interceptor.SaveAttempts;
        interceptor.Arm(conflicts: 1, rivalWrite: async () =>
        {
            await using var rival = h.Commerce();
            var level = await rival.InventoryLevels.SingleAsync(l => l.IngredientId == riceId && l.Location == null);
            level.OnHand -= 15m;
            await rival.SaveChangesAsync();
        });

        var released = await h.ProductionOrders().ReleaseAsync(order.Id);

        released.Status.Should().Be(ProductionOrderStatuses.Released);
        (interceptor.SaveAttempts - attemptsBefore).Should().Be(2); // conflicted once, then committed
        (await h.IngredientOnHandAsync(riceId)).Should().Be(5m);    // 30 − 15 rival − 10 ours, exactly once
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(5m);  // 10 − 5 ours, exactly once
    }

    [Fact]
    public async Task Release_Should_FailWithInsufficientStockAndConsumeNothing_WhenTheRecomputeFindsTheRivalTookTheStock()
    {
        var interceptor = new ConcurrencyConflictInterceptor();
        var h = new Harness(interceptor);
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 30m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        // The rival drains rice below our requirement: the recompute's FRESH pre-check must now
        // fail — proving the retry re-validates on committed state instead of re-applying the
        // stale first attempt (which would have driven stock negative).
        interceptor.Arm(conflicts: 1, rivalWrite: async () =>
        {
            await using var rival = h.Commerce();
            var level = await rival.InventoryLevels.SingleAsync(l => l.IngredientId == riceId && l.Location == null);
            level.OnHand -= 25m;
            await rival.SaveChangesAsync();
        });

        var act = async () => await h.ProductionOrders().ReleaseAsync(order.Id);
        var thrown = (await act.Should().ThrowAsync<InsufficientStockException>()).Which;
        thrown.StockItemId.Should().Be(riceId);
        thrown.Requested.Should().Be(10m);
        thrown.Available.Should().Be(5m);

        (await h.IngredientOnHandAsync(riceId)).Should().Be(5m);    // only the rival's draw stands
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(10m); // ours consumed NOTHING
        await using var ctx = h.Commerce();
        (await ctx.ProductionOrders.SingleAsync(o => o.Id == order.Id)).Status
            .Should().Be(ProductionOrderStatuses.Planned);
    }

    // ── §8 cancel: no stock effect, post-release cancel never restocks ──────────────────────────

    [Fact]
    public async Task Cancel_AfterRelease_Should_NotRestoreConsumedStock()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);

        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));
        await h.ProductionOrders().ReleaseAsync(order.Id);

        var cancelled = await h.ProductionOrders().CancelAsync(order.Id, "power outage");

        // The documented v1 posture (spec Open): the ingredients left the shelf at release;
        // reconciliation is an explicit Spec 052 stock adjustment, never a silent auto-restore.
        cancelled.Status.Should().Be(ProductionOrderStatuses.Cancelled);
        cancelled.Notes.Should().Contain("power outage");
        (await h.IngredientOnHandAsync(riceId)).Should().Be(10m);
        (await h.IngredientOnHandAsync(tomatoId)).Should().Be(5m);
    }

    [Fact]
    public async Task Cancel_Should_ClampTheAppendedNotesToTheColumnMax_KeepingTheReasonVisible()
    {
        var h = new Harness();
        var (variantId, _, _) = await h.SeedJollofAsync();
        // Near-max existing notes + a long reason: the combined append would overflow the 1024-char
        // Notes column (a SQL-Server-only failure — InMemory never enforces lengths), so the
        // service clamps the COMBINED value, truncating the OLDEST content and keeping the newest
        // tail — the cancel reason — visible.
        var nearMaxNotes = new string('n', 1_000);
        var longReason = new string('r', 200);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 4m) }, Notes: nearMaxNotes));

        var cancelled = await h.ProductionOrders().CancelAsync(order.Id, longReason);

        cancelled.Status.Should().Be(ProductionOrderStatuses.Cancelled);
        cancelled.Notes!.Length.Should().BeLessThanOrEqualTo(1_024);
        cancelled.Notes.Should().StartWith("…");                      // oldest content truncated away
        cancelled.Notes.Should().Contain($"Cancelled: {longReason}"); // the reason stays visible

        await using var ctx = h.Commerce();
        (await ctx.ProductionOrders.SingleAsync(o => o.Id == order.Id)).Notes.Should().Be(cancelled.Notes);
    }

    [Fact]
    public async Task Cancel_Should_Throw_WhenTheOrderIsCompleted()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 4m) }));
        await h.ProductionOrders().ReleaseAsync(order.Id);
        await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(order.Id, YieldFinishedGoods: false));

        var act = async () => await h.ProductionOrders().CancelAsync(order.Id);
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*is Completed*");
    }

    // ── §10 complete: produced quantities + the optional finished-good yield ────────────────────

    [Fact]
    public async Task Complete_Should_DefaultProducedToPlanned_AndYieldFinishedGoodStock()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));
        await h.ProductionOrders().ReleaseAsync(order.Id);

        h.Clock.UtcNow = new DateTime(2026, 7, 6, 14, 0, 0, DateTimeKind.Utc);
        var completed = await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(order.Id));

        completed.Status.Should().Be(ProductionOrderStatuses.Completed);
        completed.CompletedAt.Should().Be(h.Clock.UtcNow);
        completed.Lines.Single().ProducedQuantity.Should().Be(40m); // defaults to planned

        // Make-to-stock (§10): the finished variant's on-hand rose by the produced portions, and
        // the terminal flip + produced quantities were committed with it (assert via a fresh scope).
        (await h.VariantOnHandAsync(variantId)).Should().Be(40m);
        await using var ctx = h.Commerce();
        var persisted = await ctx.ProductionOrders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);
        persisted.Status.Should().Be(ProductionOrderStatuses.Completed);
        persisted.Lines.Single().ProducedQuantity.Should().Be(40m);
    }

    [Fact]
    public async Task Complete_Should_RecordExplicitActuals_AndSkipTheYield_WhenTheFlagIsOff()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));
        await h.ProductionOrders().ReleaseAsync(order.Id);
        var lineId = order.Lines.Single().Id;

        // 38 of 40 planned portions actually made (the §18 planned ≠ produced variance case),
        // cooked to order — the yield must NOT inflate sellable stock.
        var completed = await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(
            order.Id,
            new[] { new ProducedQuantityLine(lineId, 38m) },
            YieldFinishedGoods: false));

        completed.Lines.Single().ProducedQuantity.Should().Be(38m);
        (await h.VariantOnHandAsync(variantId)).Should().Be(0m); // untouched
    }

    [Fact]
    public async Task Complete_Should_GuardStatusAndActuals()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        // Planned cannot complete — the run never consumed its ingredients (§8).
        var actPlanned = async () => await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(order.Id));
        (await actPlanned.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*is Planned*only a Released or InProgress*");

        await h.ProductionOrders().ReleaseAsync(order.Id);

        // An actual for a line of a DIFFERENT order is rejected.
        var actForeign = async () => await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(
            order.Id, new[] { new ProducedQuantityLine(Guid.NewGuid(), 10m) }));
        (await actForeign.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*does not belong*");

        // A negative actual is rejected (0 is legal — a failed batch).
        var actNegative = async () => await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(
            order.Id, new[] { new ProducedQuantityLine(order.Lines.Single().Id, -1m) }));
        await actNegative.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Start_Should_MoveReleasedToInProgress_AndCompleteWorksFromThere()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);
        var order = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 40m) }));

        // Start requires Released — a Planned run has not consumed its ingredients yet (§8).
        var actPlanned = async () => await h.ProductionOrders().StartAsync(order.Id);
        (await actPlanned.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*is Planned*only a Released*");

        await h.ProductionOrders().ReleaseAsync(order.Id);
        var started = await h.ProductionOrders().StartAsync(order.Id);
        started.Status.Should().Be(ProductionOrderStatuses.InProgress);

        // InProgress carries no stock effect and completes normally.
        var completed = await h.ProductionOrders().CompleteAsync(new CompleteProductionOrderCommand(order.Id));
        completed.Status.Should().Be(ProductionOrderStatuses.Completed);
        (await h.VariantOnHandAsync(variantId)).Should().Be(40m);
    }

    // ── §11/§8 reads ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task KitchenSheet_Should_ReturnNull_WhenTheOrderDoesNotExist()
    {
        var h = new Harness();
        (await h.ProductionOrders().GetKitchenSheetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task List_Should_FilterByStatus_MostRecentPlannedForFirst_AsSummaryRows()
    {
        var h = new Harness();
        var (variantId, riceId, tomatoId) = await h.SeedJollofAsync();
        await h.SetIngredientStockAsync(riceId, 20m);
        await h.SetIngredientStockAsync(tomatoId, 10m);

        var monday = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            new DateTime(2026, 7, 6, 6, 0, 0, DateTimeKind.Utc), new[] { new ProductionOrderLineCommand(variantId, 4m) }));
        var friday = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            new DateTime(2026, 7, 10, 6, 0, 0, DateTimeKind.Utc), new[] { new ProductionOrderLineCommand(variantId, 8m) }));
        await h.ProductionOrders().ReleaseAsync(monday.Id);

        var all = await h.ProductionOrders().ListAsync();
        all.TotalCount.Should().Be(2);
        all.PageNumber.Should().Be(1);
        all.PageSize.Should().Be(20);
        all.Items.Select(o => o.Id).Should().ContainInOrder(friday.Id, monday.Id); // planned-for desc

        // Summary rows carry the header + a line count — never the per-line snapshots (the §11
        // kitchen sheet is the heavy read, mirroring the 053 OrderSummary list convention).
        var fridayRow = all.Items.Single(o => o.Id == friday.Id);
        fridayRow.Status.Should().Be(ProductionOrderStatuses.Planned);
        fridayRow.PlannedFor.Should().Be(new DateTime(2026, 7, 10, 6, 0, 0, DateTimeKind.Utc));
        fridayRow.LineCount.Should().Be(1);
        all.Items.Single(o => o.Id == monday.Id).ReleasedAt.Should().NotBeNull();

        var planned = await h.ProductionOrders().ListAsync(ProductionOrderStatuses.Planned);
        planned.TotalCount.Should().Be(1);
        planned.Items.Should().ContainSingle(o => o.Id == friday.Id);

        var released = await h.ProductionOrders().ListAsync(ProductionOrderStatuses.Released);
        released.Items.Should().ContainSingle(o => o.Id == monday.Id);
    }

    [Fact]
    public async Task List_Should_PageDeterministically_WithTheIdTieBreak_AndNormalizeOutOfRangePaging()
    {
        var h = new Harness();
        var (variantId, _, _) = await h.SeedJollofAsync();

        // Three runs planned for the SAME instant and created at the SAME (frozen) clock instant:
        // PlannedFor and CreatedAt are full ties, so only the Id tie-break makes the page walk
        // deterministic — without it a walk could skip or double-count a run between page queries
        // (the CoreOrderService.ListAsync discipline the Spec 053 purchase-order list rides).
        var a = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 1m) }));
        var b = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 2m) }));
        var c = await h.ProductionOrders().CreateAsync(new CreateProductionOrderCommand(
            PlannedFor, new[] { new ProductionOrderLineCommand(variantId, 3m) }));
        var expectedWalk = new[] { a.Id, b.Id, c.Id }.OrderBy(id => id).ToList(); // ties break Id-ascending

        var page1 = await h.ProductionOrders().ListAsync(status: null, pageNumber: 1, pageSize: 2);
        var page2 = await h.ProductionOrders().ListAsync(status: null, pageNumber: 2, pageSize: 2);

        page1.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page1.HasNextPage.Should().BeTrue();
        page2.TotalCount.Should().Be(3);
        page2.Items.Should().HaveCount(1);
        page2.HasNextPage.Should().BeFalse();
        // The walk covers every run exactly once, in the deterministic tie-broken order.
        page1.Items.Select(o => o.Id).Concat(page2.Items.Select(o => o.Id)).Should().Equal(expectedWalk);

        // Out-of-range paging resets to the defaults (the 053 NormalizePaging convention).
        var normalized = await h.ProductionOrders().ListAsync(status: null, pageNumber: 0, pageSize: 500);
        normalized.PageNumber.Should().Be(1);
        normalized.PageSize.Should().Be(20);
        normalized.Items.Should().HaveCount(3);
    }
}
