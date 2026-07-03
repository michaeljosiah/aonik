using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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
/// The Spec 054 receiving flow over <see cref="CommerceDbContext"/>: resolve-or-resume by
/// idempotency key → validate everything (incl. the 051 cost-window rules) → persist the receipt
/// (Posted) → post-claim over-receipt re-validation (deterministic: earlier claim wins, a losing
/// receipt VOIDS itself) → stock up (052) → cost refresh (051) → alert resolve (052/054) → PO
/// transition (041). MUTATION ORDERING IS THE GUARD: the receipt row — persisted FIRST under the
/// unique (TenantId, IdempotencyKey) index, before any stock or cost moves — is the applied-once
/// claim. <c>StockAppliedAt</c>/<c>CostAppliedAt</c> markers ride the first downstream SaveChanges
/// of each step (all composed services share this ONE scoped <see cref="CommerceDbContext"/>, per
/// <c>CommerceModule</c>'s scoped registrations), so a keyed retry of a receive that crashed
/// post-claim RESUMES exactly the steps that never applied — never re-applying, never
/// double-counting stock (the spec's High risk). <c>PayloadHash</c> pins the key to one logical
/// receive: a reused key with different lines conflicts instead of silently returning the stored
/// receipt. The PO transition still rides the separate Ordering context, so the flow remains a
/// sequence of commits, not one transaction — the same two-context reality Spec 053 documented.
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

    /// <summary>
    /// Test-only interleaving seam: awaited between the pre-claim validation snapshot and the
    /// claim persist — exactly where a concurrent rival's claim lands in the §8 race. Lets tests
    /// commit a rival receipt inside the window deterministically; always null in production
    /// (internal, settable only by tests via InternalsVisibleTo).
    /// </summary>
    internal Func<CancellationToken, Task>? OnBeforeClaimForTests { get; set; }

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

        // The payload fingerprint the key is pinned to (§8): PO + the normalized lines. A keyed
        // retry must describe the SAME logical receive to resolve/resume; anything else conflicts.
        var payloadHash = ComputePayloadHash(command.PurchaseOrderId, lines);

        // ── 0. RESOLVE by key BEFORE any mutation — and before the PO-status guard (§8/R7) ───────
        // A lost-response retry of a receive that COMPLETED its PO would otherwise be rejected by
        // the Pending guard below even though the receive succeeded (the same lesson Spec 053
        // learned for its shortfall seed). An existing receipt means the claim already happened:
        // verify the key really describes the same receive, then RESUME whatever the crash left
        // unapplied (tracked read — the resume mutates the marker columns).
        var existing = await FindByKeyAsync(tenantId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureKeyedHitMatches(existing, command.PurchaseOrderId, payloadHash, idempotencyKey);
            return await ResumeAsync(existing, tenantId, cancellationToken);
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
        var orderedByIngredient = OrderedByIngredient(po);

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
        // policy is the spec's Open follow-up). This pre-claim pass fast-fails the sequential case
        // with clean figures; the POST-claim re-validation below is the concurrency authority.
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

        // ── 0. PRE-CLAIM COST VALIDATION (051 rules, §10) ─────────────────────────────────────────
        // Every cost-carrying line must be WRITABLE before anything is claimed: a backdated
        // ReceivedAt that lands inside a fully elapsed cost window (Spec 051's history-rewrite
        // guard) would be refused by SetCostAsync AFTER stock had already moved — so the same rule
        // seam runs here first, rejecting the whole receive up-front with nothing applied.
        var receivedAt = command.ReceivedAt ?? _clock.UtcNow;
        foreach (var line in lines.Where(l => l.UnitCostActual is not null))
        {
            await _ingredientCosts.ValidateSetCostAsync(
                new SetIngredientCostCommand(line.IngredientId, po.CurrencyIn, line.UnitCostActual!.Value, EffectiveFrom: receivedAt),
                cancellationToken);
        }

        if (OnBeforeClaimForTests is not null)
        {
            await OnBeforeClaimForTests(cancellationToken);
        }

        // ── 1. PERSIST the receipt + lines, Posted — the idempotency CLAIM (first commit) ────────
        // From here on a retry with this key resumes THIS receipt, whatever happens below: better
        // a receipt whose downstream effects need one resumed retry than stock counted twice.
        // Currency is stamped from the PO's CurrencyIn on cost-carrying lines — Spec 051's
        // SetCostAsync requires one, and the PO is what the price was agreed in.
        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = po.Id,
            IdempotencyKey = idempotencyKey,
            PayloadHash = payloadHash,
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
            // rows and arbitrate on the winner: a voided winner or a different payload still
            // conflicts, and a matching winner is returned WITHOUT resuming — the rival call that
            // just claimed it is live and applying its own effects; racing it here could
            // double-apply. (A retry after that rival call dies lands on the pre-claim resolve
            // path above, which resumes.)
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
            EnsureKeyedHitMatches(winner, command.PurchaseOrderId, payloadHash, idempotencyKey);
            return await MapAsync(winner, tenantId, Array.Empty<Guid>(), 0, cancellationToken);
        }

        // ── 1½. RE-VALIDATE over-receipt POST-claim — the deterministic winner (§8/§9) ───────────
        // Two receives with DIFFERENT keys can both pass the pre-claim sum (neither sees the
        // other's uncommitted claim) and both claim. So the cumulative check re-runs on committed
        // state: all non-voided receipts of the PO, ordered by (CreatedAt, Id), counting only
        // receipts up to and including THIS one in that order. Every racer computes the same
        // order, so the earlier claim always wins and the later one VOIDS ITSELF — audit-preserved,
        // excluded from every future sum, applying no stock or cost.
        var cumulative = await SumNonVoidedAsync(tenantId, po.Id, receipt, cancellationToken);
        var breaches = lines
            .Select(l => new
            {
                l.IngredientId,
                Ordered = orderedByIngredient[l.IngredientId],
                Counted = cumulative.UpToInclusive.GetValueOrDefault(l.IngredientId),
                This = l.QuantityReceived,
            })
            .Where(x => x.Counted > x.Ordered)
            .ToList();
        if (breaches.Count > 0)
        {
            receipt.Status = GoodsReceiptStatuses.Voided;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(
                "Receiving would exceed the ordered quantity for: " +
                string.Join("; ", breaches.Select(x =>
                    $"'{DisplayName(names, x.IngredientId)}' (ordered {x.Ordered}, received by earlier receipts {x.Counted - x.This}, this receipt {x.This})")) +
                ". A concurrent receipt claimed first (earlier claims win deterministically); this receipt was VOIDED " +
                "and applied nothing — no stock or cost moved. Verify what actually arrived and retry the corrected " +
                "quantity under a NEW idempotency key.");
        }

        // ── 2. STOCK ▲ — increment each ingredient's default-location on-hand (052) ──────────────
        await ApplyStockAsync(receipt, receiptLines, cancellationToken);

        // ── 3. COST ↻ — a line carrying an actual cost is also a landed-cost refresh (051, §10):
        // a NEW effective-dated IngredientCost row from ReceivedAt; the prior row (a manually set
        // standard cost, or an earlier receipt's) is closed off, never mutated.
        var costRowsWritten = await ApplyCostsAsync(receipt, receiptLines, cancellationToken);

        // ── 4+5. TAIL — alert resolve (§8/R4) + PO completion, recomputed from committed state ───
        // (shares the post-claim re-sum above — Total spans ALL non-voided posted receipts).
        var (resolvedAlertIds, poAfter) = await RunTailAsync(
            po, lineIngredientIds, orderedByIngredient, cumulative.Total, cancellationToken);

        return await MapAsync(receipt, tenantId, resolvedAlertIds, costRowsWritten, cancellationToken, poAfter);
    }

    // ── the §8 steps, shared by the fresh path and the keyed-retry resume ───────────────────────

    /// <summary>
    /// Resumes an already claimed receipt (§8/R7): re-runs stock only if <c>StockAppliedAt</c> is
    /// null and cost only if <c>CostAppliedAt</c> is null — a crash between the claim and either
    /// step left the marker null, so the keyed retry applies exactly the missing effects, once —
    /// then ALWAYS re-runs the idempotent tail (alert resolve + completion recompute). A retry of
    /// a fully applied receipt therefore reports no new work but returns the same live-recomputed
    /// response a fresh receive would.
    /// </summary>
    private async Task<GoodsReceiptDto> ResumeAsync(GoodsReceipt receipt, Guid tenantId, CancellationToken cancellationToken)
    {
        // The PERSISTED lines are the source of truth (hash-equal to the retry's lines anyway).
        var receiptLines = await _dbContext.GoodsReceiptLines
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.GoodsReceiptId == receipt.Id)
            .ToListAsync(cancellationToken);
        var lineIngredientIds = receiptLines.Select(l => l.IngredientId).ToList();

        if (receipt.StockAppliedAt is null)
        {
            await ApplyStockAsync(receipt, receiptLines, cancellationToken);
        }
        var costRowsWritten = 0;
        if (receipt.CostAppliedAt is null)
        {
            costRowsWritten = await ApplyCostsAsync(receipt, receiptLines, cancellationToken);
        }

        var po = await _orders.GetAsync(receipt.PurchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order '{receipt.PurchaseOrderId}' was not found.");
        var totals = await SumReceivedAsync(tenantId, receipt.PurchaseOrderId, cancellationToken);
        var (resolvedAlertIds, poAfter) = await RunTailAsync(
            po, lineIngredientIds, OrderedByIngredient(po), totals, cancellationToken);

        return await MapAsync(receipt, tenantId, resolvedAlertIds, costRowsWritten, cancellationToken, poAfter);
    }

    /// <summary>
    /// Increments each line's ingredient on-hand (052). The <c>StockAppliedAt</c> marker is set on
    /// the TRACKED receipt BEFORE the first increment: the inventory service saves through this
    /// same scoped <see cref="CommerceDbContext"/>, so the first increment's SaveChanges commits
    /// marker + stock atomically — the marker can never claim stock that did not land. A crash
    /// between later lines of a multi-line receipt leaves the remainder under-applied behind a set
    /// marker (visible against the receipt, reconciled by hand at worst) — the deliberate §8
    /// failure direction; never double-counted.
    /// </summary>
    private async Task ApplyStockAsync(GoodsReceipt receipt, IReadOnlyList<GoodsReceiptLine> receiptLines, CancellationToken cancellationToken)
    {
        receipt.StockAppliedAt = _clock.UtcNow;
        foreach (var line in receiptLines)
        {
            await _inventory.AdjustOnHandAsync(
                StockItemRef.Ingredient(line.IngredientId), line.QuantityReceived, cancellationToken);
        }
    }

    /// <summary>
    /// Writes the effective-dated cost refresh for every cost-carrying line (051, §10). Same
    /// marker pattern as stock: <c>CostAppliedAt</c> is set on the tracked receipt before the
    /// writes and rides the first cost row's SaveChanges on the shared context; when NO line
    /// carries a cost the marker is persisted explicitly so the resume discriminator stays
    /// deterministic.
    /// </summary>
    private async Task<int> ApplyCostsAsync(GoodsReceipt receipt, IReadOnlyList<GoodsReceiptLine> receiptLines, CancellationToken cancellationToken)
    {
        receipt.CostAppliedAt = _clock.UtcNow;
        var costRowsWritten = 0;
        foreach (var line in receiptLines.Where(l => l.UnitCostActual is not null))
        {
            await _ingredientCosts.SetCostAsync(
                new SetIngredientCostCommand(line.IngredientId, line.Currency!, line.UnitCostActual!.Value, EffectiveFrom: receipt.ReceivedAt),
                cancellationToken);
            costRowsWritten++;
        }
        if (costRowsWritten == 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        return costRowsWritten;
    }

    /// <summary>
    /// The idempotent tail, ALWAYS run — fresh receive, crash resume, or plain retry: resolve
    /// recovered low-stock alerts (§8/R4) and complete the PO when every ordered ingredient is
    /// fully received across ALL non-voided posted receipts (§9). Partial receipt stays DERIVED,
    /// never a status.
    /// </summary>
    private async Task<(IReadOnlyList<Guid> ResolvedAlertIds, OrderDto PoAfter)> RunTailAsync(
        OrderDto po,
        IReadOnlyList<Guid> ingredientIds,
        Dictionary<Guid, decimal> orderedByIngredient,
        Dictionary<Guid, decimal> totalReceived,
        CancellationToken cancellationToken)
    {
        // ALERT ✓ — resolve ONLY on demonstrated recovery (§8/R4): the service recomputes live
        // available vs the reorder point; a short receipt still at/below threshold leaves the
        // alert (Open/Acknowledged/Ordered) exactly as it was.
        var resolvedAlertIds = await _lowStockAlerts.ResolveIfRecoveredAsync(ingredientIds, cancellationToken);

        // PO → — completion is recomputed from COMMITTED receipt lines (all non-voided posted
        // receipts of the PO), never from this call's in-memory view, so a sibling receipt that
        // claimed concurrently counts and the last observer completes the PO.
        var fullyReceived = orderedByIngredient.All(kv => totalReceived.GetValueOrDefault(kv.Key) >= kv.Value);
        var poAfter = po;
        if (fullyReceived && string.Equals(po.Status, OrderStatusCodes.Pending, StringComparison.Ordinal))
        {
            poAfter = await TransitionPoToCompleteAsync(po.Id, cancellationToken);
        }
        return (resolvedAlertIds, poAfter);
    }

    /// <summary>
    /// The §8/R5 completion transition with the observed Pending as the compare-and-set
    /// expectation. A mismatch where the order is ALREADY <c>Complete</c> is SUCCESS — a
    /// concurrent sibling receipt won the transition to the same terminal state; any other
    /// mismatch (an operator cancel racing this receipt) rethrows loudly rather than resurrecting
    /// a terminal PO — the goods are still real: receipt, stock, and cost all stand. Internal so
    /// tests can drive the sibling interleaving directly.
    /// </summary>
    internal async Task<OrderDto> TransitionPoToCompleteAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        try
        {
            return await _orders.TransitionAsync(
                purchaseOrderId, OrderStatusCodes.Complete, "Fully received",
                expectedFromStatus: OrderStatusCodes.Pending, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var current = await _orders.GetAsync(purchaseOrderId, cancellationToken);
            if (current is not null && string.Equals(current.Status, OrderStatusCodes.Complete, StringComparison.Ordinal))
            {
                return current; // the sibling receipt reached the same outcome first
            }
            throw;
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Canonical payload fingerprint (§8): SHA-256 hex over the PO id + the NORMALIZED lines
    /// (ingredient, quantity, unit cost) ordered by ingredient id — so line order, whitespace, and
    /// trailing decimal zeros never change the identity of a receive. Internal so tests can stamp
    /// seeded intermediate-state receipts with the exact hash a retry recomputes.
    /// </summary>
    internal static string ComputePayloadHash(Guid purchaseOrderId, IEnumerable<ReceiveGoodsLineCommand> normalizedLines)
    {
        var canonical = new StringBuilder(purchaseOrderId.ToString("D"));
        foreach (var line in normalizedLines.OrderBy(l => l.IngredientId))
        {
            canonical
                .Append('|').Append(line.IngredientId.ToString("D"))
                .Append(':').Append(line.QuantityReceived.ToString("0.####", CultureInfo.InvariantCulture))
                .Append(':').Append(line.UnitCostActual?.ToString("0.####", CultureInfo.InvariantCulture) ?? "-");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    /// <summary>
    /// Guards every keyed hit (§8): a VOIDED receipt surfaces its over-receipt conflict (the key
    /// is spent — retrying it must never read as success), and a key reused for a different PO or
    /// different lines conflicts instead of silently returning a receipt for goods the caller
    /// never described.
    /// </summary>
    private static void EnsureKeyedHitMatches(GoodsReceipt existing, Guid requestedPurchaseOrderId, string payloadHash, string idempotencyKey)
    {
        if (string.Equals(existing.Status, GoodsReceiptStatuses.Voided, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Goods receipt for idempotency key '{idempotencyKey}' was VOIDED: it lost a concurrent over-receipt " +
                $"race on purchase order '{existing.PurchaseOrderId}' and applied nothing. Verify what actually " +
                "arrived and submit the corrected receipt under a NEW idempotency key.");
        }
        if (existing.PurchaseOrderId != requestedPurchaseOrderId)
        {
            throw new InvalidOperationException(
                $"Idempotency key '{idempotencyKey}' was already used for a receipt on purchase order " +
                $"'{existing.PurchaseOrderId}' and cannot be reused for purchase order '{requestedPurchaseOrderId}'. " +
                "A key identifies ONE logical receive — use a new key for a new receive.");
        }
        if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Idempotency key '{idempotencyKey}' was reused with a DIFFERENT payload: the stored receipt on " +
                $"purchase order '{existing.PurchaseOrderId}' was recorded with different lines. A key identifies " +
                "ONE logical receive — retry with the original lines to resume it, or use a new key for a new receive.");
        }
    }

    /// <summary>TRACKED on purpose: a keyed hit resumes by mutating the marker columns.</summary>
    private Task<GoodsReceipt?> FindByKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken)
        => _dbContext.GoodsReceipts
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IdempotencyKey == idempotencyKey, cancellationToken);

    private static Dictionary<Guid, decimal> OrderedByIngredient(OrderDto po)
        => po.Items
            .Where(i => i.ProductId is not null)
            .GroupBy(i => i.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity ?? 0m));

    /// <summary>Received-so-far per ingredient, summed over every NON-VOIDED receipt for the PO
    /// (§9) — the explicit received-vs-ordered tracking that keeps a half-delivered PO unambiguous.
    /// A voided receipt applied nothing, so it never counts anywhere.</summary>
    private async Task<Dictionary<Guid, decimal>> SumReceivedAsync(Guid tenantId, Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        var receiptIds = await _dbContext.GoodsReceipts
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.PurchaseOrderId == purchaseOrderId
                && r.Status != GoodsReceiptStatuses.Voided)
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

    /// <summary>Both cumulative views the post-claim steps need, from one committed-state read:
    /// <see cref="UpToInclusive"/> counts receipts up to and including the anchor in (CreatedAt,
    /// Id) order — the deterministic over-receipt arbiter; <see cref="Total"/> spans all
    /// non-voided posted receipts — the completion recompute.</summary>
    private sealed record CumulativeReceived(Dictionary<Guid, decimal> UpToInclusive, Dictionary<Guid, decimal> Total);

    /// <summary>
    /// The post-claim re-sum (§8): per-ingredient received over the PO's NON-VOIDED receipts,
    /// sliced two ways. Every concurrent claimant computes the same (CreatedAt, Id) order over
    /// committed rows, so exactly the claims that fit the ordered quantity — in claim order — win,
    /// and any later claim that would breach sees itself over the line and voids itself.
    /// </summary>
    private async Task<CumulativeReceived> SumNonVoidedAsync(Guid tenantId, Guid purchaseOrderId, GoodsReceipt anchor, CancellationToken cancellationToken)
    {
        var receipts = await _dbContext.GoodsReceipts
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.PurchaseOrderId == purchaseOrderId
                && r.Status != GoodsReceiptStatuses.Voided)
            .Select(r => new { r.Id, r.CreatedAt })
            .ToListAsync(cancellationToken);

        // Ordered IN MEMORY so every racer applies identical (CreatedAt, then Id tie-break)
        // semantics regardless of database collation quirks.
        var orderedReceipts = receipts.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
        var anchorPosition = orderedReceipts.FindIndex(r => r.Id == anchor.Id);
        var upToIds = orderedReceipts.Take(anchorPosition + 1).Select(r => r.Id).ToHashSet();
        var allIds = orderedReceipts.Select(r => r.Id).ToList();

        var perReceipt = await _dbContext.GoodsReceiptLines
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && allIds.Contains(l.GoodsReceiptId))
            .GroupBy(l => new { l.GoodsReceiptId, l.IngredientId })
            .Select(g => new { g.Key.GoodsReceiptId, g.Key.IngredientId, Total = g.Sum(x => x.QuantityReceived) })
            .ToListAsync(cancellationToken);

        var upToInclusive = perReceipt
            .Where(x => upToIds.Contains(x.GoodsReceiptId))
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        var total = perReceipt
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        return new CumulativeReceived(upToInclusive, total);
    }

    /// <summary>
    /// Builds the receipt DTO from CURRENT state: cumulative received re-summed across all
    /// non-voided receipts (this one included), on-hand from the live default-location levels, and
    /// the PO status as it stands — so the fresh path and an idempotent retry read the same way.
    /// <paramref name="resolvedAlertIds"/>/<paramref name="costRowsWritten"/> stay call-scoped: a
    /// retry that applied nothing honestly reports nothing.
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

        var orderedByIngredient = OrderedByIngredient(po);
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
