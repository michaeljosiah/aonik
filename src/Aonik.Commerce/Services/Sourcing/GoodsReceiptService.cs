using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// The Spec 054 receiving flow over <see cref="CommerceDbContext"/>: resolve-or-create by
/// idempotency key → persist the receipt (Posted) → stock up (052) → cost refresh (051) → alert
/// resolve (052/054) → PO transition (041). MUTATION ORDERING IS THE GUARD: the receipt row —
/// persisted FIRST under the unique (TenantId, IdempotencyKey) index, before any stock or cost
/// moves — is the applied-once claim, so every retry direction is safe. The composed services each
/// own their SaveChanges (and the PO transition commits on the Ordering context), so this is a
/// sequence of commits, not one transaction — the same two-context reality Spec 053 documented for
/// its alert flip. The failure direction is deliberate: all validation runs BEFORE the claim, so a
/// post-claim failure is infrastructure-only and leaves a Posted receipt whose keyed retry returns
/// it without re-applying — the system fails toward under-application (visible: stock/cost short
/// of the receipt), never toward double-counted stock (the spec's High risk).
/// </summary>
internal sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly CommerceDbContext _dbContext;
    private readonly IOrderService _orders;
    private readonly IInventoryService _inventory;
    private readonly IIngredientCostService _ingredientCosts;
    private readonly ILowStockAlertService _lowStockAlerts;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public GoodsReceiptService(
        CommerceDbContext dbContext,
        IOrderService orders,
        IInventoryService inventory,
        IIngredientCostService ingredientCosts,
        ILowStockAlertService lowStockAlerts,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _orders = orders;
        _inventory = inventory;
        _ingredientCosts = ingredientCosts;
        _lowStockAlerts = lowStockAlerts;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<GoodsReceiptDto> ReceiveAsync(ReceiveGoodsCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // ── Command shape (no reads, no writes) ─────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException(
                "An idempotency key is required — it is what makes a retried receive safe (Spec 054 §8).",
                nameof(command));
        }
        // Same normalization as the Order spine's key handling: a whitespace-padded retry resolves
        // to the receipt the original call stored.
        var idempotencyKey = command.IdempotencyKey.Trim();

        if (command.Lines is null || command.Lines.Count == 0)
        {
            throw new ArgumentException("A goods receipt requires at least one line.", nameof(command));
        }
        // One line per ingredient per receipt: duplicates would write two same-instant cost rows
        // ambiguously and blur the cumulative sums. (A second delivery is a second receipt.)
        var duplicated = command.Lines.GroupBy(l => l.IngredientId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicated.Count > 0)
        {
            throw new ArgumentException(
                "A goods receipt carries one line per ingredient; duplicated: " +
                $"{string.Join(", ", duplicated.Select(id => $"'{id}'"))}. Consolidate the quantities into one line.",
                nameof(command));
        }
        // Validated on the NORMALIZED values (the 053 convention): quantities/costs are stored at
        // the decimal(19,4) column scale, so a raw value that rounds to zero is rejected here
        // rather than silently becoming a zero line.
        var lines = command.Lines
            .Select(l => new ReceiveGoodsLineCommand(
                l.IngredientId,
                Math.Round(l.QuantityReceived, 4, MidpointRounding.AwayFromZero),
                l.UnitCostActual is { } cost ? Math.Round(cost, 4, MidpointRounding.AwayFromZero) : null))
            .ToList();
        if (lines.Any(l => l.QuantityReceived <= 0m))
        {
            throw new ArgumentException(
                "Every received quantity must be positive (in the ingredient's base unit).", nameof(command));
        }
        if (lines.Any(l => l.UnitCostActual is <= 0m))
        {
            throw new ArgumentException("An actual unit cost must be positive when given.", nameof(command));
        }

        // ── 0. RESOLVE by key BEFORE any mutation — and before the PO-status guard (§8/R7) ───────
        // A lost-response retry of a receive that COMPLETED its PO would otherwise be rejected by
        // the Pending guard below even though the receive succeeded (the same lesson Spec 053
        // learned for its shortfall seed). An existing receipt means the effects were already
        // claimed: return it, re-applying nothing.
        var existing = await FindByKeyAsync(tenantId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await MapAsync(existing, tenantId, resolvedAlertIds: Array.Empty<Guid>(), costRowsWritten: 0, cancellationToken);
        }

        // ── 0. GUARD the PO (053 shape over the spine) ────────────────────────────────────────────
        var po = await _orders.GetAsync(command.PurchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order '{command.PurchaseOrderId}' was not found.");
        if (!string.Equals(po.OrderType, OrderTypeCodes.PurchaseOrder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Order '{po.Id}' is a {po.OrderType} order, not a purchase order.");
        }
        // Pending = submitted to the supplier (the landed 053 mapping onto the existing
        // OrderStatusCodes). A Draft PO was never placed; Complete/Cancelled are terminal.
        if (!string.Equals(po.Status, OrderStatusCodes.Pending, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Purchase order '{po.Id}' is {po.Status}; only a submitted (Pending) purchase order can be received.");
        }

        // Ordered per ingredient — the PO line's ProductId is the documented IngredientId soft-ref
        // (Spec 053 §10); summed per ingredient so duplicate PO lines can never split the check.
        var orderedByIngredient = po.Items
            .Where(i => i.ProductId is not null)
            .GroupBy(i => i.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity ?? 0m));

        var lineIngredientIds = lines.Select(l => l.IngredientId).ToList();
        var names = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && lineIngredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);

        var strays = lines.Where(l => !orderedByIngredient.ContainsKey(l.IngredientId)).ToList();
        if (strays.Count > 0)
        {
            throw new InvalidOperationException(
                $"Not on purchase order '{po.Id}': " +
                $"{string.Join(", ", strays.Select(l => $"'{DisplayName(names, l.IngredientId)}'"))}. " +
                "A goods receipt can only receive the ingredients the purchase order ordered.");
        }

        // OVER-RECEIPT (v1 tolerance = none): cumulative received across ALL receipts for this PO —
        // prior receipts plus this one — must not exceed ordered, per ingredient (§9; tolerance
        // policy is the spec's Open follow-up).
        var priorByIngredient = await SumReceivedAsync(tenantId, po.Id, cancellationToken);
        var over = lines
            .Select(l => new
            {
                l.IngredientId,
                Ordered = orderedByIngredient[l.IngredientId],
                Prior = priorByIngredient.GetValueOrDefault(l.IngredientId),
                This = l.QuantityReceived,
            })
            .Where(x => x.Prior + x.This > x.Ordered)
            .ToList();
        if (over.Count > 0)
        {
            throw new InvalidOperationException(
                "Receiving would exceed the ordered quantity for: " +
                string.Join("; ", over.Select(x =>
                    $"'{DisplayName(names, x.IngredientId)}' (ordered {x.Ordered}, already received {x.Prior}, this receipt {x.This})")) +
                ". Over-receipt is not accepted in v1.");
        }

        // ── 1. PERSIST the receipt + lines, Posted — the idempotency CLAIM (first commit) ────────
        // From here on a retry with this key returns THIS receipt, whatever happens below: better
        // a receipt whose downstream effects need one manual reconciliation than stock counted
        // twice. Currency is stamped from the PO's CurrencyIn on cost-carrying lines — Spec 051's
        // SetCostAsync requires one, and the PO is what the price was agreed in.
        var receivedAt = command.ReceivedAt ?? _clock.UtcNow;
        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = po.Id,
            IdempotencyKey = idempotencyKey,
            ReceivedAt = receivedAt,
            Status = GoodsReceiptStatuses.Posted,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
        };
        var receiptLines = lines
            .Select(l => new GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GoodsReceiptId = receipt.Id,
                IngredientId = l.IngredientId,
                QuantityReceived = l.QuantityReceived,
                UnitCostActual = l.UnitCostActual,
                Currency = l.UnitCostActual is null ? null : po.CurrencyIn,
            })
            .ToList();
        _dbContext.GoodsReceipts.Add(receipt);
        _dbContext.GoodsReceiptLines.AddRange(receiptLines);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost an idempotency race: a concurrent receive with the same key committed first and
            // tripped the unique (TenantId, IdempotencyKey) index (SQL Server; the pre-check above
            // is the tested path — InMemory does not enforce unique indexes). Detach our rejected
            // rows and return the winner — its call applies the effects, ours applies none.
            _dbContext.Entry(receipt).State = EntityState.Detached;
            foreach (var line in receiptLines)
            {
                _dbContext.Entry(line).State = EntityState.Detached;
            }
            var winner = await FindByKeyAsync(tenantId, idempotencyKey, cancellationToken);
            if (winner is null)
            {
                throw; // not the key race — surface the real constraint failure
            }
            return await MapAsync(winner, tenantId, Array.Empty<Guid>(), 0, cancellationToken);
        }

        // ── 2. STOCK ▲ — increment each ingredient's default-location on-hand (052) ──────────────
        foreach (var line in receiptLines)
        {
            await _inventory.AdjustOnHandAsync(
                StockItemRef.Ingredient(line.IngredientId), line.QuantityReceived, cancellationToken);
        }

        // ── 3. COST ↻ — a line carrying an actual cost is also a landed-cost refresh (051, §10):
        // a NEW effective-dated IngredientCost row from ReceivedAt; the prior row (a manually set
        // standard cost, or an earlier receipt's) is closed off, never mutated.
        var costRowsWritten = 0;
        foreach (var line in receiptLines.Where(l => l.UnitCostActual is not null))
        {
            await _ingredientCosts.SetCostAsync(
                new SetIngredientCostCommand(line.IngredientId, line.Currency!, line.UnitCostActual!.Value, EffectiveFrom: receivedAt),
                cancellationToken);
            costRowsWritten++;
        }

        // ── 4. ALERT ✓ — resolve ONLY on demonstrated recovery (§8/R4): the service recomputes
        // live available vs the reorder point; a short receipt still at/below threshold leaves the
        // alert (Open/Acknowledged/Ordered) exactly as it was.
        var resolvedAlertIds = await _lowStockAlerts.ResolveIfRecoveredAsync(
            receiptLines.Select(l => l.IngredientId).ToList(), cancellationToken);

        // ── 5. PO → — Complete only when EVERY ordered ingredient is fully received across all
        // receipts; else the PO stays Pending, open for the next delivery (partial receipt is
        // DERIVED, never a status — §9). The observed Pending travels as the compare-and-set
        // expectation, so an interleaved transition (an operator cancel racing this receipt) fails
        // the transition loudly rather than resurrecting a terminal PO — the goods are still real:
        // receipt, stock, and cost above all stand.
        var fullyReceived = orderedByIngredient.All(kv =>
            priorByIngredient.GetValueOrDefault(kv.Key)
                + receiptLines.Where(l => l.IngredientId == kv.Key).Sum(l => l.QuantityReceived)
            >= kv.Value);
        var poAfter = fullyReceived
            ? await _orders.TransitionAsync(
                po.Id, OrderStatusCodes.Complete, "Fully received",
                expectedFromStatus: OrderStatusCodes.Pending, cancellationToken)
            : po;

        return await MapAsync(receipt, tenantId, resolvedAlertIds, costRowsWritten, cancellationToken, poAfter);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    private Task<GoodsReceipt?> FindByKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken)
        => _dbContext.GoodsReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IdempotencyKey == idempotencyKey, cancellationToken);

    /// <summary>Received-so-far per ingredient, summed over every receipt for the PO (§9) — the
    /// explicit received-vs-ordered tracking that keeps a half-delivered PO unambiguous.</summary>
    private async Task<Dictionary<Guid, decimal>> SumReceivedAsync(Guid tenantId, Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        var receiptIds = await _dbContext.GoodsReceipts
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.PurchaseOrderId == purchaseOrderId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (receiptIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var sums = await _dbContext.GoodsReceiptLines
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && receiptIds.Contains(l.GoodsReceiptId))
            .GroupBy(l => l.IngredientId)
            .Select(g => new { IngredientId = g.Key, Total = g.Sum(x => x.QuantityReceived) })
            .ToListAsync(cancellationToken);
        return sums.ToDictionary(x => x.IngredientId, x => x.Total);
    }

    /// <summary>
    /// Builds the receipt DTO from CURRENT state: cumulative received re-summed across all
    /// receipts (this one included), on-hand from the live default-location levels, and the PO
    /// status as it stands — so the fresh path and an idempotent retry read the same way.
    /// <paramref name="resolvedAlertIds"/>/<paramref name="costRowsWritten"/> stay call-scoped: a
    /// retry applied nothing and honestly reports nothing.
    /// </summary>
    private async Task<GoodsReceiptDto> MapAsync(
        GoodsReceipt receipt,
        Guid tenantId,
        IReadOnlyList<Guid> resolvedAlertIds,
        int costRowsWritten,
        CancellationToken cancellationToken,
        OrderDto? po = null)
    {
        po ??= await _orders.GetAsync(receipt.PurchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order '{receipt.PurchaseOrderId}' was not found.");

        var lines = await _dbContext.GoodsReceiptLines
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.GoodsReceiptId == receipt.Id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
        var ingredientIds = lines.Select(l => l.IngredientId).ToList();

        var orderedByIngredient = po.Items
            .Where(i => i.ProductId is not null)
            .GroupBy(i => i.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity ?? 0m));
        var cumulative = await SumReceivedAsync(tenantId, receipt.PurchaseOrderId, cancellationToken);

        var names = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);
        var onHand = await _dbContext.InventoryLevels
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && l.IngredientId != null
                && ingredientIds.Contains(l.IngredientId!.Value)
                && l.Location == null)
            .ToDictionaryAsync(l => l.IngredientId!.Value, l => l.OnHand, cancellationToken);

        return new GoodsReceiptDto(
            receipt.Id,
            receipt.PurchaseOrderId,
            receipt.IdempotencyKey,
            receipt.Status,
            receipt.ReceivedAt,
            receipt.Notes,
            lines
                .Select(l => new GoodsReceiptLineDto(
                    l.Id,
                    l.IngredientId,
                    names.GetValueOrDefault(l.IngredientId),
                    l.QuantityReceived,
                    l.UnitCostActual,
                    l.Currency,
                    orderedByIngredient.GetValueOrDefault(l.IngredientId),
                    cumulative.GetValueOrDefault(l.IngredientId),
                    onHand.GetValueOrDefault(l.IngredientId)))
                .ToList(),
            po.Status,
            string.Equals(po.Status, OrderStatusCodes.Complete, StringComparison.Ordinal),
            resolvedAlertIds,
            costRowsWritten);
    }

    private static string DisplayName(IReadOnlyDictionary<Guid, string> names, Guid ingredientId)
        => names.TryGetValue(ingredientId, out var name) ? name : ingredientId.ToString();
}
