using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>Effective-dated ingredient costs over <see cref="CommerceDbContext"/> (Spec 051 §8).</summary>
internal sealed class IngredientCostService : IIngredientCostService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public IngredientCostService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<IngredientCostDto> SetCostAsync(SetIngredientCostCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // ONE rule seam (Spec 054 FIX): every guard — ingredient exists/active, positive cost,
        // currency, and the window/history-rewrite check — lives in EnsureSetCostAllowedAsync,
        // shared verbatim with the no-write ValidateSetCostAsync that composing flows (the goods
        // receipt) run BEFORE claiming anything.
        var (currency, effectiveFrom) = await EnsureSetCostAllowedAsync(tenantId, command, cancellationToken);

        // The tail = the single open row (EffectiveTo == null) for (tenant, ingredient, currency).
        var tail = await _dbContext.IngredientCosts
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                    && c.IngredientId == command.IngredientId
                    && c.Currency == currency
                    && c.EffectiveTo == null,
                cancellationToken);

        // Windows are contiguous half-open [EffectiveFrom, EffectiveTo). The new row opens unless
        // effectiveFrom lands before a scheduled row — then the containing window is SPLIT and the
        // new row is inserted already closed at the successor's start (§8).
        DateTime? effectiveTo = null;

        if (tail is not null && effectiveFrom >= tail.EffectiveFrom)
        {
            // On/after the tail's start: close the tail AT the new cost's effective date (§8/R2).
            // For an immediate reprice that is "now"; for a future-dated (scheduled) cost the tail
            // keeps pricing until the boundary — date-aware GetCurrentCost still returns it (R4).
            // An equal EffectiveFrom is allowed: it zero-widths the tail — a same-instant
            // correction that keeps the row on record.
            tail.EffectiveTo = effectiveFrom;
            tail.IsActive = false;
        }
        else if (tail is not null)
        {
            // effectiveFrom lands BEFORE the tail's start, i.e. at least one scheduled row starts
            // after it. Scheduling a future cost must not lock out correcting the window that is
            // pricing today (§8), so split the window containing effectiveFrom, leaving the tail —
            // and every other later row — untouched. The rewrite guards already ran in
            // EnsureSetCostAllowedAsync; the defensive throw below only trips if the timeline
            // changed between the validation read and this tracked read.
            var containing = await _dbContext.IngredientCosts
                .Where(c => c.TenantId == tenantId
                    && c.IngredientId == command.IngredientId
                    && c.Currency == currency
                    && c.EffectiveFrom <= effectiveFrom)
                .OrderByDescending(c => c.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Cost history changed concurrently: no window contains {effectiveFrom:u} anymore. Retry the write.");

            // The earliest row starting after effectiveFrom — at minimum the tail. It bounds the
            // new (closed) window and is preserved as-is, so the tail remains the single open row
            // and the filtered unique index invariant (§12) is never in play.
            var successor = await _dbContext.IngredientCosts
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId
                    && c.IngredientId == command.IngredientId
                    && c.Currency == currency
                    && c.EffectiveFrom > effectiveFrom)
                .OrderBy(c => c.EffectiveFrom)
                .FirstAsync(cancellationToken);

            // Split: the containing row now ends where the correction begins, and the correction
            // runs up to the untouched successor. Same-instant convention as the tail path: an
            // equal EffectiveFrom zero-widths the containing row.
            containing.EffectiveTo = effectiveFrom;
            containing.IsActive = false;
            effectiveTo = successor.EffectiveFrom;
        }

        var cost = new IngredientCost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IngredientId = command.IngredientId,
            Currency = currency,
            UnitCost = command.UnitCost,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            IsActive = effectiveTo is null,
        };
        _dbContext.IngredientCosts.Add(cost);

        // Close-or-split-prior + insert in one transaction; the DB filtered unique index on
        // (TenantId, IngredientId, Currency) WHERE EffectiveTo IS NULL is the concurrency
        // backstop (§8/§12 — SQL Server only; InMemory tests cover the service invariant).
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(cost);
    }

    public async Task ValidateSetCostAsync(SetIngredientCostCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureSetCostAllowedAsync(tenantId, command, cancellationToken);
    }

    /// <summary>
    /// The single validation seam behind <see cref="SetCostAsync"/> and
    /// <see cref="ValidateSetCostAsync"/> — no writes, no tracking. Throws exactly what the write
    /// would: missing currency, non-positive cost, unknown/deactivated ingredient, and the §8
    /// history-rewrite guard (an <c>EffectiveFrom</c> inside a window that already finished pricing
    /// the past, or before all recorded history, is refused — only the currently-effective window
    /// or a future one may be corrected). Returns the normalized currency and resolved effective
    /// date for the write path to reuse.
    /// </summary>
    private async Task<(string Currency, DateTime EffectiveFrom)> EnsureSetCostAllowedAsync(
        Guid tenantId, SetIngredientCostCommand command, CancellationToken cancellationToken)
    {
        var currency = NormalizeCurrency(command.Currency);
        if (command.UnitCost <= 0m)
        {
            throw new ArgumentException("Unit cost must be positive.");
        }

        var ingredient = await _dbContext.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId && i.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Ingredient '{command.IngredientId}' was not found.");
        if (!ingredient.IsActive)
        {
            throw new InvalidOperationException(
                $"Ingredient '{ingredient.Name}' is deactivated and cannot be costed.");
        }

        var effectiveFrom = command.EffectiveFrom ?? _clock.UtcNow;

        // The tail = the single open row (EffectiveTo == null) for (tenant, ingredient, currency).
        // On/after its start the write simply closes it — always allowed. Before its start we are
        // splitting an earlier window, and the HISTORY-REWRITE GUARD (§8) applies: a window that
        // already finished pricing the past is immutable — only the currently-effective window
        // (the one date-effective at "now") or a future window may be corrected. Because windows
        // are contiguous, a fully elapsed containing window is exactly an effectiveFrom earlier
        // than the start of the currently-effective window. Before all recorded history there is
        // no window to split at all.
        var tail = await _dbContext.IngredientCosts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                    && c.IngredientId == command.IngredientId
                    && c.Currency == currency
                    && c.EffectiveTo == null,
                cancellationToken);
        if (tail is null || effectiveFrom >= tail.EffectiveFrom)
        {
            return (currency, effectiveFrom);
        }

        var containing = await _dbContext.IngredientCosts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                && c.IngredientId == command.IngredientId
                && c.Currency == currency
                && c.EffectiveFrom <= effectiveFrom)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        // The earliest row starting after effectiveFrom — at minimum the tail. It bounds the new
        // (closed) window in the write path and anchors the "history starts at" message here.
        var successor = await _dbContext.IngredientCosts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                && c.IngredientId == command.IngredientId
                && c.Currency == currency
                && c.EffectiveFrom > effectiveFrom)
            .OrderBy(c => c.EffectiveFrom)
            .FirstAsync(cancellationToken);

        if (containing is null)
        {
            throw new InvalidOperationException(
                $"Cannot set a cost effective {effectiveFrom:u} — no cost window for '{ingredient.Name}' in {currency} " +
                $"contains that date; recorded history starts at {successor.EffectiveFrom:u}. " +
                "Use an effective date inside the currently-effective window or later.");
        }
        if (containing.EffectiveTo is not null && containing.EffectiveTo <= _clock.UtcNow)
        {
            throw new InvalidOperationException(
                $"Cannot set a cost effective {effectiveFrom:u} for '{ingredient.Name}' in {currency} — that window " +
                $"closed at {containing.EffectiveTo:u} and has already priced the past. Only the currently-effective " +
                "window or a future window can be corrected.");
        }

        return (currency, effectiveFrom);
    }

    public async Task<IngredientCostDto?> GetCurrentCostAsync(Guid ingredientId, string currency, DateTime? atUtc = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalized = NormalizeCurrency(currency);
        var at = atUtc ?? _clock.UtcNow;

        // DATE-AWARE window resolution (§8/R3): the row whose [EffectiveFrom, EffectiveTo) window
        // contains atUtc, newest first. IsActive is never the selector — a scheduled (future
        // EffectiveFrom) row does not price "now" (R4).
        var row = await _dbContext.IngredientCosts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                && c.IngredientId == ingredientId
                && c.Currency == normalized
                && c.EffectiveFrom <= at
                && (c.EffectiveTo == null || at < c.EffectiveTo))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<IngredientCostDto>> ListHistoryAsync(Guid ingredientId, string? currency = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.IngredientCosts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IngredientId == ingredientId);

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalized = NormalizeCurrency(currency);
            query = query.Where(c => c.Currency == normalized);
        }

        var rows = await query
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required (ISO 4217, e.g. NGN).");
        }
        return currency.Trim().ToUpperInvariant();
    }

    private static IngredientCostDto Map(IngredientCost c)
        => new(c.Id, c.IngredientId, c.Currency, c.UnitCost, c.EffectiveFrom, c.EffectiveTo, c.IsActive);
}
