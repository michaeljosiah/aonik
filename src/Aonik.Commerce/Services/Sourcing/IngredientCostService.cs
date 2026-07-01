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

        var open = await _dbContext.IngredientCosts
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                    && c.IngredientId == command.IngredientId
                    && c.Currency == currency
                    && c.EffectiveTo == null,
                cancellationToken);

        if (open is not null)
        {
            // Backdating before the open row's start would invert its [EffectiveFrom, EffectiveTo)
            // window and silently rewrite resolved history (§8). An equal EffectiveFrom is allowed:
            // it zero-widths the row — a same-instant correction that keeps the row on record.
            if (effectiveFrom < open.EffectiveFrom)
            {
                throw new InvalidOperationException(
                    $"Cannot set a cost effective {effectiveFrom:u} — a cost for '{ingredient.Name}' in {currency} " +
                    $"already takes effect at {open.EffectiveFrom:u}. Use an effective date on or after it.");
            }

            // Close the prior open row AT the new cost's effective date (§8/R2). For an immediate
            // reprice that is "now"; for a future-dated (scheduled) cost the prior row keeps
            // pricing until the boundary — date-aware GetCurrentCost still returns it (R4).
            open.EffectiveTo = effectiveFrom;
            open.IsActive = false;
        }

        var cost = new IngredientCost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IngredientId = command.IngredientId,
            Currency = currency,
            UnitCost = command.UnitCost,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            IsActive = true,
        };
        _dbContext.IngredientCosts.Add(cost);

        // Close-prior + open-new in one transaction; the DB filtered unique index on
        // (TenantId, IngredientId, Currency) WHERE EffectiveTo IS NULL is the concurrency
        // backstop (§8/§12 — SQL Server only; InMemory tests cover the service invariant).
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(cost);
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
