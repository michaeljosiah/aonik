using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Production;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Production / work orders over <see cref="CommerceDbContext"/> (Spec 056). The lifecycle is
/// service-owned (§8): each transition validates the current status first, and the two
/// stock-moving edges are single-shot — release consumes exactly once on Planned → Released,
/// completion yields exactly once on the terminal flip.
///
/// RELEASE (§9) adapts the spec's fan-out sketch to the landed inventory API honestly: the spec
/// names <c>InventoryService.CommitAsync(ingredient, qty, ref)</c>, but the landed commit is the
/// second half of a reserve-then-commit two-phase (two SaveChanges, a TTL'd hold in between) whose
/// soft-hold semantics the spec itself defers ("Reserve at Planned" is an Open decision — v1
/// consumes only at release). So release consumes DIRECTLY: the whole-bill availability pre-check
/// (Available = OnHand − Reserved, honouring live checkout holds) and every tracked level
/// decrement plus the status flip ride ONE SaveChanges on the shared scoped Commerce context —
/// single-commit all-or-nothing, with the global rowversion token as the oversell guard. On a
/// concurrency conflict the attempt is rolled back IN FULL (every dirty entry reloaded) and
/// recomputed from scratch — never a partial re-apply, which could double-decrement — bounded to
/// three attempts, mirroring the Spec 054 retry discipline.
///
/// COMPLETE (§10) rides the Spec 054 marker pattern: produced quantities and the terminal flip are
/// staged on the tracked order BEFORE the per-variant <c>AdjustOnHandAsync</c> yields, so the
/// first increment's SaveChanges (same scoped context) commits status + first yield atomically. A
/// crash mid-yield leaves a Completed run with an under-applied yield — visible and reconciled by
/// a stock adjustment, never double-counted (the same failure direction Spec 054 chose).
/// </summary>
internal sealed class ProductionOrderService : IProductionOrderService
{
    private const string UnknownProductName = "(unknown product)";
    private const string UnknownVariantName = "(unknown variant)";

    /// <summary>Must match the <c>Notes</c> max length in <c>ProductionOrderConfiguration</c> —
    /// SQL Server rejects an overflow at SaveChanges (InMemory does not enforce it).</summary>
    private const int NotesMaxLength = 1024;

    private readonly CommerceDbContext _dbContext;
    private readonly IRecipeService _recipes;
    private readonly IProductionPlanningService _planning;
    private readonly IInventoryService _inventory;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public ProductionOrderService(
        CommerceDbContext dbContext,
        IRecipeService recipes,
        IProductionPlanningService planning,
        IInventoryService inventory,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _recipes = recipes;
        _planning = planning;
        _inventory = inventory;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    // ── create (§7): freeze the per-portion snapshot per line ──────────────────────────────────

    public async Task<ProductionOrderDto> CreateAsync(CreateProductionOrderCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (command.Lines is not { Count: > 0 })
        {
            throw new ArgumentException("A production order requires at least one line.", nameof(command));
        }
        // Validated on the NORMALIZED value (the 053 convention): quantities are stored at the
        // decimal(19,4) column scale, so a raw value that rounds to zero is rejected here rather
        // than silently becoming a zero-portion dish.
        if (command.Lines.Any(l => NormalizeQuantity(l.PlannedQuantity) <= 0m))
        {
            throw new ArgumentException("Every line's planned quantity must be positive (portions).", nameof(command));
        }

        // Duplicate variant entries are merged (quantities summed) — one dish per variant on the
        // kitchen sheet, mirroring how SetRecipe merges duplicate components.
        var demands = command.Lines
            .GroupBy(l => l.ProductVariantId)
            .Select(g => (VariantId: g.Key, Portions: NormalizeQuantity(g.Sum(l => l.PlannedQuantity))))
            .ToList();

        // Variants must EXIST on this create path (the operator named them); the from-sheet path
        // trusts the sheet's committed demand instead.
        var variantNames = await LoadVariantDisplayNamesAsync(tenantId, demands.Select(d => d.VariantId), cancellationToken);
        var missing = demands.Where(d => !variantNames.ContainsKey(d.VariantId)).Select(d => d.VariantId).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Product variant(s) not found: {string.Join(", ", missing.Select(id => $"'{id}'"))}.");
        }

        // Explode each line ONCE at one portion and freeze it (§7/R9). A variant with no active
        // recipe rejects the whole create, naming it — a line never carries an empty snapshot.
        var seeds = new List<ProductionSeed>();
        var withoutRecipe = new List<Guid>();
        foreach (var demand in demands)
        {
            var explosion = await _recipes.ExplodeAsync(demand.VariantId, 1m, cancellationToken);
            if (!explosion.HasActiveRecipe)
            {
                withoutRecipe.Add(demand.VariantId);
                continue;
            }
            seeds.Add(new ProductionSeed(demand.VariantId, demand.Portions, SerializeSnapshot(explosion), null, null, null));
        }
        if (withoutRecipe.Count > 0)
        {
            throw new InvalidOperationException(
                "No active recipe exists for: " +
                $"{string.Join(", ", withoutRecipe.Select(id => $"'{variantNames.GetValueOrDefault(id, id.ToString())}'"))}. " +
                "A production line is exploded through its recipe — define one (SetRecipe) before planning the run.");
        }

        var order = await PersistAsync(tenantId, command.PlannedFor, seeds, command.Notes, cancellationToken);
        return Map(order);
    }

    public async Task<ProductionOrderFromSheetDto> CreateFromProductionSheetAsync(CreateFromProductionSheetCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // The Spec 055 sheet is the demand source — same window semantics, same §9 inclusion
        // filter, bundle lines already expanded into their component variants.
        var sheet = await _planning.GetProductionSheetAsync(new ProductionWindow(command.FromUtc, command.ToUtc), cancellationToken);
        if (sheet.Lines.Count == 0)
        {
            throw new InvalidOperationException(
                $"The production sheet for [{command.FromUtc:O}, {command.ToUtc:O}) holds no demand; there is nothing to seed.");
        }

        // Seed the variants that CAN be exploded; skip and REPORT the rest (§7). The sheet
        // legitimately contains no-recipe variants (Spec 055 surfaces the same diagnostic), so a
        // whole-seed rejection would make the feature unusable — but a silent drop would
        // under-produce, so the skips always travel with the result.
        var seeds = new List<ProductionSeed>();
        var skipped = new List<Guid>();
        foreach (var line in sheet.Lines)
        {
            var explosion = await _recipes.ExplodeAsync(line.ProductVariantId, 1m, cancellationToken);
            if (!explosion.HasActiveRecipe)
            {
                skipped.Add(line.ProductVariantId);
                continue;
            }
            // Spec 068 §9 — a sheet line IS a (variant, personalisation) demand group; the trio
            // rides onto the production line so the kitchen sheet can render the preparation.
            seeds.Add(new ProductionSeed(
                line.ProductVariantId, NormalizeQuantity(line.PortionsDemanded), SerializeSnapshot(explosion),
                line.PersonalisationJson, line.PersonalisationSummary, line.PersonalisationDisplayJson));
        }
        if (seeds.Count == 0)
        {
            throw new InvalidOperationException(
                "None of the demanded variants on the production sheet has an active recipe " +
                $"({string.Join(", ", skipped.Select(id => $"'{id}'"))}); there is nothing to seed. " +
                "Define recipes (SetRecipe) or create the production order from explicit lines.");
        }

        var order = await PersistAsync(tenantId, command.PlannedFor ?? command.FromUtc, seeds, command.Notes, cancellationToken);
        return new ProductionOrderFromSheetDto(Map(order), skipped);
    }

    // ── release (§9): the consume edge — one all-or-nothing commit ─────────────────────────────

    public async Task<ProductionOrderDto> ReleaseAsync(Guid productionOrderId, CancellationToken cancellationToken = default)
    {
        // Bounded recompute-from-scratch retry (§9/R3): a rowversion conflict on any touched row
        // rolls the WHOLE attempt back — every dirty tracked entry (the staged decrements AND the
        // status flip) is reloaded to committed state — and the next attempt re-reads the order
        // (a rival release that won turns this call into the idempotent no-op), re-merges the bill
        // from the frozen snapshots (deterministic), and re-validates availability on FRESH
        // levels. Never a partial re-apply: reapplying deltas over a half-reloaded graph could
        // double-decrement. Exhaustion rethrows so the caller fails loudly, never silently.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ReleaseOnceAsync(productionOrderId, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                foreach (var entry in _dbContext.ChangeTracker.Entries()
                    .Where(e => e.State is not (EntityState.Unchanged or EntityState.Detached))
                    .ToList())
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }
    }

    private async Task<ProductionOrderDto> ReleaseOnceAsync(Guid productionOrderId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var order = await GetTrackedOrderAsync(tenantId, productionOrderId, cancellationToken);

        // Idempotent on the target edge (§8/R4): re-releasing a Released run is a no-op — stock is
        // never double-consumed (the spec's central correctness property). Every other non-Planned
        // status conflicts.
        if (string.Equals(order.Status, ProductionOrderStatuses.Released, StringComparison.Ordinal))
        {
            return Map(order);
        }
        if (!string.Equals(order.Status, ProductionOrderStatuses.Planned, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production order '{productionOrderId}' is {order.Status}; only a Planned production order can be released.");
        }

        // The bill is merged from the FROZEN per-line snapshots (§7/§9) — per-portion × planned,
        // summed per ingredient — never a live re-explosion, so release consumes exactly what the
        // printed kitchen sheet showed even if the Spec 050 recipe was edited in between.
        var bill = MergeBill(order.Lines);

        // Resolve the TRACKED default-location ingredient levels (Spec 052 semantics:
        // Location == null). A missing level is zero stock; release never creates levels, so a
        // shortfall leaves no phantom rows behind.
        var ingredientIds = bill.Select(b => b.IngredientId).ToList();
        var levels = (await _dbContext.InventoryLevels
                .Where(l => l.TenantId == tenantId
                    && l.IngredientId != null
                    && ingredientIds.Contains(l.IngredientId!.Value)
                    && l.Location == null)
                .ToListAsync(cancellationToken))
            .ToDictionary(l => l.IngredientId!.Value);

        // Pass 1 — validate EVERY ingredient's availability first (fail fast, nothing applied):
        // Available = OnHand − Reserved, so live checkout holds are honoured, never cannibalised.
        foreach (var line in bill)
        {
            var available = levels.TryGetValue(line.IngredientId, out var level)
                ? level.OnHand - level.Reserved
                : 0m;
            if (available < line.RequiredQuantity)
            {
                throw new InsufficientStockException(
                    StockItemRef.Ingredient(line.IngredientId), line.RequiredQuantity, available);
            }
        }

        // Pass 2 — apply every decrement AND the status flip; ONE SaveChanges commits them
        // all-or-nothing (the rowversion token on each level is the Spec 042 oversell guard —
        // two racing releases cannot both draw the same stock).
        foreach (var line in bill)
        {
            levels[line.IngredientId].OnHand -= line.RequiredQuantity;
        }
        order.Status = ProductionOrderStatuses.Released;
        order.ReleasedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    // ── start (§8): optional sub-state, no stock effect ────────────────────────────────────────

    public async Task<ProductionOrderDto> StartAsync(Guid productionOrderId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var order = await GetTrackedOrderAsync(tenantId, productionOrderId, cancellationToken);

        if (!string.Equals(order.Status, ProductionOrderStatuses.Released, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production order '{productionOrderId}' is {order.Status}; only a Released production order can be started.");
        }

        order.Status = ProductionOrderStatuses.InProgress;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(order);
    }

    // ── complete (§10): the yield edge ──────────────────────────────────────────────────────────

    public async Task<ProductionOrderDto> CompleteAsync(CompleteProductionOrderCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var order = await GetTrackedOrderAsync(tenantId, command.ProductionOrderId, cancellationToken);

        if (order.Status is not (ProductionOrderStatuses.Released or ProductionOrderStatuses.InProgress))
        {
            throw new InvalidOperationException(
                $"Production order '{command.ProductionOrderId}' is {order.Status}; " +
                "only a Released or InProgress production order can be completed.");
        }

        // Validate + normalize the explicit actuals (keyed by line id; 0 records a failed batch).
        var actualByLine = new Dictionary<Guid, decimal>();
        foreach (var actual in command.ActualQuantities ?? Array.Empty<ProducedQuantityLine>())
        {
            var quantity = NormalizeQuantity(actual.ProducedQuantity);
            if (quantity < 0m)
            {
                throw new ArgumentException(
                    $"Produced quantity for line '{actual.ProductionOrderLineId}' cannot be negative.", nameof(command));
            }
            if (order.Lines.All(l => l.Id != actual.ProductionOrderLineId))
            {
                throw new InvalidOperationException(
                    $"Line '{actual.ProductionOrderLineId}' does not belong to production order '{order.Id}'.");
            }
            if (!actualByLine.TryAdd(actual.ProductionOrderLineId, quantity))
            {
                throw new ArgumentException(
                    $"Line '{actual.ProductionOrderLineId}' appears more than once in the actual quantities.", nameof(command));
            }
        }

        // Stage EVERYTHING first — the Spec 054 marker pattern: the produced quantities and the
        // terminal flip ride the FIRST yield increment's SaveChanges on the shared scoped
        // CommerceDbContext, so a completed status can never claim a yield that did not land.
        foreach (var line in order.Lines)
        {
            line.ProducedQuantity = actualByLine.TryGetValue(line.Id, out var actual) ? actual : line.PlannedQuantity;
        }
        order.Status = ProductionOrderStatuses.Completed;
        order.CompletedAt = _clock.UtcNow;

        if (command.YieldFinishedGoods)
        {
            // Make-to-stock (§10): finished goods enter sellable stock as a signed INCREMENT (the
            // Spec 054 AdjustOnHandAsync path with its commute-safe bounded retry) — never an
            // overwrite, so a racing checkout commit or receipt loses nothing.
            var yielded = false;
            foreach (var line in order.Lines.Where(l => l.ProducedQuantity is > 0m))
            {
                await _inventory.AdjustOnHandAsync(
                    StockItemRef.Variant(line.ProductVariantId), line.ProducedQuantity!.Value, cancellationToken);
                yielded = true;
            }
            if (!yielded)
            {
                // Every line yielded zero (an all-failed batch): no increment saved the staged
                // changes — persist them explicitly.
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(order);
    }

    // ── cancel (§8): no stock effect; post-release cancel never restocks ────────────────────────

    public async Task<ProductionOrderDto> CancelAsync(Guid productionOrderId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var order = await GetTrackedOrderAsync(tenantId, productionOrderId, cancellationToken);

        if (order.Status is not (ProductionOrderStatuses.Planned or ProductionOrderStatuses.Released or ProductionOrderStatuses.InProgress))
        {
            throw new InvalidOperationException(
                $"Production order '{productionOrderId}' is {order.Status}; " +
                "only a Planned, Released, or InProgress production order can be cancelled.");
        }

        // Cancelling AFTER release does NOT auto-restore consumed stock (spec Open, deferred):
        // the ingredients physically left the shelf at release, and silently re-adding them would
        // mask real usage — the operator path is an explicit Spec 052 stock adjustment.
        order.Status = ProductionOrderStatuses.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            var note = $"Cancelled: {reason.Trim()}";
            var combined = string.IsNullOrWhiteSpace(order.Notes) ? note : $"{order.Notes}\n{note}";
            // The append is clamped to the column max: near-max existing notes + a long reason
            // would otherwise overflow at SaveChanges (a SQL-Server-only 500). The OLDEST content
            // is truncated away — the cancel reason is the newest, operationally live tail and
            // must stay visible.
            order.Notes = combined.Length <= NotesMaxLength
                ? combined
                : $"…{combined[^(NotesMaxLength - 1)..]}";
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(order);
    }

    // ── kitchen sheet (§11): a pure read over the frozen snapshots ──────────────────────────────

    public async Task<KitchenSheetDto?> GetKitchenSheetAsync(Guid productionOrderId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var order = await _dbContext.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == productionOrderId && o.TenantId == tenantId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        // Names come from the live catalog with LEFT-join semantics (the Spec 055 convention) —
        // a variant that no longer resolves still shows with placeholder names, never dropped.
        var names = await ResolveVariantNamesAsync(
            tenantId, order.Lines.Select(l => l.ProductVariantId).Distinct().ToList(), cancellationToken);

        var dishes = order.Lines
            .Select(line =>
            {
                var (productName, variantName) = names.TryGetValue(line.ProductVariantId, out var resolved)
                    ? resolved
                    : (UnknownProductName, UnknownVariantName);
                var components = DeserializeSnapshot(line)
                    .Select(c => new KitchenSheetComponentDto(
                        c.IngredientId, c.IngredientName, c.BaseUnit, c.QuantityPerPortion,
                        c.QuantityPerPortion * line.PlannedQuantity))
                    .ToList();
                return new KitchenSheetDishDto(
                    line.Id, line.ProductVariantId, productName, variantName,
                    line.PlannedQuantity, line.ProducedQuantity, components,
                    line.PersonalisationSummary, line.PersonalisationDisplayJson);
            })
            .OrderBy(d => d.ProductName, StringComparer.Ordinal)
            .ThenBy(d => d.VariantName, StringComparer.Ordinal)
            .ThenBy(d => d.ProductVariantId)
            .ThenBy(d => d.PersonalisationSummary ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        // Totals = the merge of the SAME frozen snapshots — by construction identical to the bill
        // ReleaseAsync consumes (§9/§11): the sheet on the pass and the draw-down cannot diverge.
        var totals = MergeBill(order.Lines)
            .Select(b => new KitchenSheetTotalLineDto(b.IngredientId, b.IngredientName, b.BaseUnit, b.RequiredQuantity))
            .ToList();

        return new KitchenSheetDto(order.Id, order.PlannedFor, order.Status, order.Notes, dishes, totals);
    }

    public async Task<PagedResult<ProductionOrderSummaryDto>> ListAsync(
        string? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        // The spine's list convention (CoreOrderService.NormalizePaging, which the Spec 053
        // purchase-order list rides): out-of-range values reset to the defaults, never throw.
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = _dbContext.ProductionOrders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Summary rows only (the 053 OrderSummary convention): the frozen per-line snapshots are
        // the heavy payload and belong to the §11 kitchen sheet, never the board list. PlannedFor
        // and CreatedAt are not a total order — Id breaks ties deterministically so a multi-page
        // window walk never skips or double-counts a run (the CoreOrderService.ListAsync
        // discipline).
        var items = await query
            .OrderByDescending(o => o.PlannedFor)
            .ThenByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new ProductionOrderSummaryDto(
                o.Id, o.PlannedFor, o.Status, o.Notes, o.ReleasedAt, o.CompletedAt, o.Lines.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductionOrderSummaryDto>(items, totalCount, pageNumber, pageSize);
    }

    // ── internals ───────────────────────────────────────────────────────────────────────────────

    private async Task<ProductionOrder> PersistAsync(
        Guid tenantId,
        DateTime plannedFor,
        IReadOnlyList<ProductionSeed> seeds,
        string? notes,
        CancellationToken cancellationToken)
    {
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlannedFor = plannedFor,
            Status = ProductionOrderStatuses.Planned,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        };
        foreach (var seed in seeds)
        {
            order.Lines.Add(new ProductionOrderLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductionOrderId = order.Id,
                ProductVariantId = seed.VariantId,
                PlannedQuantity = seed.Portions,
                RecipeSnapshotJson = seed.SnapshotJson,
                PersonalisationJson = seed.PersonalisationJson,
                PersonalisationSummary = seed.PersonalisationSummary,
                PersonalisationDisplayJson = seed.PersonalisationDisplayJson,
            });
        }
        _dbContext.ProductionOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return order;
    }

    private async Task<ProductionOrder> GetTrackedOrderAsync(Guid tenantId, Guid productionOrderId, CancellationToken cancellationToken)
        => await _dbContext.ProductionOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == productionOrderId && o.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Production order '{productionOrderId}' was not found.");

    /// <summary>The per-portion snapshot frozen at creation (§7/R9): Spec 050's explosion at one
    /// portion, component order already name-stable.</summary>
    private static string SerializeSnapshot(RecipeExplosionDto explosion)
        => JsonSerializer.Serialize(explosion.Lines
            .Select(l => new RecipeSnapshotComponent(l.IngredientId, l.IngredientName, l.BaseUnit, l.RequiredQuantity))
            .ToList());

    private static IReadOnlyList<RecipeSnapshotComponent> DeserializeSnapshot(ProductionOrderLine line)
    {
        var components = string.IsNullOrWhiteSpace(line.RecipeSnapshotJson)
            ? null
            : JsonSerializer.Deserialize<List<RecipeSnapshotComponent>>(line.RecipeSnapshotJson);
        if (components is not { Count: > 0 })
        {
            // Creation froze a non-empty snapshot by construction (§7); an empty or unreadable one
            // is corruption — fail loudly, never a silent under-consume (§9).
            throw new InvalidOperationException(
                $"Production-order line '{line.Id}' carries no usable recipe snapshot.");
        }
        return components;
    }

    /// <summary>The §9 bill: per-portion × planned per line, summed per ingredient across lines,
    /// name-ordered for deterministic errors and stable projections. Read ONLY from the frozen
    /// snapshots — release and the kitchen sheet both call this, which is what keeps them equal.</summary>
    private static List<(Guid IngredientId, string IngredientName, string BaseUnit, decimal RequiredQuantity)> MergeBill(
        IEnumerable<ProductionOrderLine> lines)
    {
        var byIngredient = new Dictionary<Guid, (string Name, string BaseUnit, decimal Required)>();
        foreach (var line in lines)
        {
            foreach (var component in DeserializeSnapshot(line))
            {
                var required = component.QuantityPerPortion * line.PlannedQuantity;
                byIngredient[component.IngredientId] = byIngredient.TryGetValue(component.IngredientId, out var acc)
                    ? (acc.Name, acc.BaseUnit, acc.Required + required)
                    : (component.IngredientName, component.BaseUnit, required);
            }
        }
        return byIngredient
            .Select(kvp => (kvp.Key, kvp.Value.Name, kvp.Value.BaseUnit, kvp.Value.Required))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Key)
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> LoadVariantDisplayNamesAsync(
        Guid tenantId, IEnumerable<Guid> variantIds, CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().ToList();
        return await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);
    }

    private async Task<Dictionary<Guid, (string ProductName, string VariantName)>> ResolveVariantNamesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return new Dictionary<Guid, (string ProductName, string VariantName)>();
        }

        var ids = variantIds.ToList();
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && ids.Contains(v.Id))
            .Select(v => new { v.Id, v.Name, v.ProductId })
            .ToListAsync(cancellationToken);

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return variants.ToDictionary(
            v => v.Id,
            v => (productNames.GetValueOrDefault(v.ProductId, UnknownProductName), v.Name));
    }

    private static ProductionOrderDto Map(ProductionOrder order)
        => new(
            order.Id,
            order.PlannedFor,
            order.Status,
            order.Notes,
            order.ReleasedAt,
            order.CompletedAt,
            order.Lines
                .OrderBy(l => l.CreatedAt)
                .ThenBy(l => l.Id)
                .Select(l => new ProductionOrderLineDto(
                    l.Id, l.ProductVariantId, l.PlannedQuantity, l.ProducedQuantity, DeserializeSnapshot(l)))
                .ToList());

    /// <summary>Quantities are stored at the decimal(19,4) column scale (the 053 convention);
    /// normalized at computation so the persisted value matches what the caller gets back.</summary>
    private static decimal NormalizeQuantity(decimal quantity)
        => Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
}

/// <summary>One production-line seed: a (variant, personalisation) demand group with its frozen
/// per-portion recipe snapshot (Spec 056 §7, personalisation per Spec 068 §9).</summary>
internal sealed record ProductionSeed(
    Guid VariantId,
    decimal Portions,
    string SnapshotJson,
    string? PersonalisationJson,
    string? PersonalisationSummary,
    string? PersonalisationDisplayJson);
