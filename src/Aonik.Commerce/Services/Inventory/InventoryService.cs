using Aonik.Commerce.Contracts.Models.Inventory;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Inventory;

/// <summary>
/// Stock + reservation over <see cref="CommerceDbContext"/> (Spec 042 §10), keyed by stock item —
/// variant or ingredient — per Spec 052 §8. One reservation engine for both kinds.
/// </summary>
internal sealed class InventoryService : IInventoryService
{
    /// <summary>How long a checkout hold survives before the expiry sweep frees it.</summary>
    public static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(30);

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public InventoryService(CommerceDbContext dbContext, ITenantProvider tenantProvider, ITenantContext tenantContext, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    // ── Stock-item-keyed core (Spec 052 §8) ────────────────────────────────────────────────────

    public async Task<decimal> GetAvailableAsync(StockItemRef item, CancellationToken cancellationToken = default)
    {
        ValidateKind(item);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var level = await FindDefaultLevelQuery(tenantId, item).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return level is null ? 0m : level.OnHand - level.Reserved;
    }

    public async Task<StockLevelDto> GetStockLevelAsync(StockItemRef item, CancellationToken cancellationToken = default)
    {
        ValidateKind(item);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var level = await FindDefaultLevelQuery(tenantId, item).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return level is null
            ? new StockLevelDto(item.Kind, item.Id, 0m, 0m, 0m, null, null)
            : new StockLevelDto(item.Kind, item.Id, level.OnHand, level.Reserved, level.OnHand - level.Reserved, level.ReorderPoint, level.ReorderQuantity);
    }

    public async Task SetOnHandAsync(StockItemRef item, decimal onHand, CancellationToken cancellationToken = default)
    {
        ValidateKind(item);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureIngredientExistsAsync(tenantId, item, cancellationToken);
        var level = await GetOrCreateDefaultLevelAsync(tenantId, item, cancellationToken);
        level.OnHand = onHand;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StockLevelDto> AdjustOnHandAsync(StockItemRef item, decimal delta, CancellationToken cancellationToken = default)
    {
        ValidateKind(item);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureIngredientExistsAsync(tenantId, item, cancellationToken);
        var level = await GetOrCreateDefaultLevelAsync(tenantId, item, cancellationToken);
        level.OnHand += delta;

        // Bounded rowversion-conflict retry (Spec 054 §8/R2): a signed increment COMMUTES with any
        // rival write — reloading the level pulls the rival's committed OnHand and re-applying the
        // same delta lands on the value both movements together demand, so neither is lost. Without
        // this, a receipt racing a checkout commit (or a rival receipt line) would surface a raw
        // DbUpdateConcurrencyException and drop the increment on the floor. Three attempts bound
        // pathological contention; exhaustion rethrows so the caller fails loudly, never silently.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                // Reload the level FRESH (drops our stale local value and concurrency token, pulls
                // the rival's committed row) and re-apply the signed delta on top of it.
                await _dbContext.Entry(level).ReloadAsync(cancellationToken);
                level.OnHand += delta;
            }
        }

        return new StockLevelDto(item.Kind, item.Id, level.OnHand, level.Reserved, level.OnHand - level.Reserved, level.ReorderPoint, level.ReorderQuantity);
    }

    public async Task<StockLevelDto> SetReorderPointAsync(StockItemRef item, decimal? reorderPoint, decimal? reorderQuantity = null, CancellationToken cancellationToken = default)
    {
        ValidateKind(item);
        if (reorderPoint is < 0m)
        {
            throw new ArgumentException("Reorder point cannot be negative.", nameof(reorderPoint));
        }
        if (reorderQuantity is <= 0m)
        {
            throw new ArgumentException("Reorder quantity must be positive when set.", nameof(reorderQuantity));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureIngredientExistsAsync(tenantId, item, cancellationToken);
        var level = await GetOrCreateDefaultLevelAsync(tenantId, item, cancellationToken);
        level.ReorderPoint = reorderPoint;
        level.ReorderQuantity = reorderQuantity;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StockLevelDto(item.Kind, item.Id, level.OnHand, level.Reserved, level.OnHand - level.Reserved, level.ReorderPoint, level.ReorderQuantity);
    }

    public async Task ReserveAsync(Guid holdRef, IReadOnlyCollection<InventoryReservationLine> lines, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Aggregate by stock item so the same item requested twice is checked as a single demand.
        var demand = lines
            .GroupBy(l => l.Item)
            .Select(g => (Item: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();
        foreach (var (item, _) in demand)
        {
            ValidateKind(item);
        }

        var now = _clock.UtcNow;
        var expiresAt = now.Add(ReservationTtl);

        // First pass — validate every line can be satisfied (all-or-nothing).
        var levels = new Dictionary<StockItemRef, InventoryLevel>();
        foreach (var (item, quantity) in demand)
        {
            var level = await GetOrCreateDefaultLevelAsync(tenantId, item, cancellationToken);
            var available = level.OnHand - level.Reserved;
            if (available < quantity)
            {
                throw new InsufficientStockException(item, quantity, available);
            }
            levels[item] = level;
        }

        // Second pass — apply. Nothing was persisted above (SaveChanges is here).
        foreach (var (item, quantity) in demand)
        {
            levels[item].Reserved += quantity;
            _dbContext.InventoryReservations.Add(new InventoryReservation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductVariantId = item.IsIngredient ? null : item.Id,
                IngredientId = item.IsIngredient ? item.Id : null,
                StockItemKind = item.Kind,
                HoldRef = holdRef,
                Quantity = quantity,
                Status = InventoryReservationStatuses.Held,
                ExpiresAt = expiresAt,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CommitAsync(Guid holdRef, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var held = await _dbContext.InventoryReservations
            .Where(r => r.TenantId == tenantId && r.HoldRef == holdRef && r.Status == InventoryReservationStatuses.Held)
            .ToListAsync(cancellationToken);

        foreach (var reservation in held)
        {
            var level = await GetOrCreateDefaultLevelAsync(tenantId, ToStockItemRef(reservation), cancellationToken);
            level.OnHand -= reservation.Quantity;
            level.Reserved -= reservation.Quantity;
            reservation.Status = InventoryReservationStatuses.Committed;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid holdRef, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var held = await _dbContext.InventoryReservations
            .Where(r => r.TenantId == tenantId && r.HoldRef == holdRef && r.Status == InventoryReservationStatuses.Held)
            .ToListAsync(cancellationToken);

        foreach (var reservation in held)
        {
            var level = await GetOrCreateDefaultLevelAsync(tenantId, ToStockItemRef(reservation), cancellationToken);
            level.Reserved -= reservation.Quantity;
            reservation.Status = InventoryReservationStatuses.Released;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ReleaseExpiredAsync(DateTime? asOfUtc = null, CancellationToken cancellationToken = default)
    {
        var at = asOfUtc ?? _clock.UtcNow;
        // Global sweep — the Worker runs this without a tenant ambient. Read across tenants, then
        // write per tenant: AonikDbContextBase.EnforceTenantOnWrites() requires a resolved tenant for
        // any modified ITenantScoped row, so we set the tenant context for each group before saving.
        // AcrossTenants() is IgnoreQueryFilters(), which also drops the soft-delete filter — exclude deleted rows explicitly.
        var expired = await _dbContext.InventoryReservations.AcrossTenants()
            .Where(r => !r.IsDeleted && r.Status == InventoryReservationStatuses.Held && r.ExpiresAt <= at)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        var originalTenant = _tenantContext.TenantId;
        var originalSource = _tenantContext.ResolutionSource;
        try
        {
            foreach (var group in expired.GroupBy(r => r.TenantId))
            {
                _tenantContext.TenantId = group.Key;
                _tenantContext.ResolutionSource = "inventory-sweep";

                foreach (var reservation in group)
                {
                    // Match the level on whichever id the hold carries (Spec 052 §8).
                    var variantId = reservation.ProductVariantId;
                    var ingredientId = reservation.IngredientId;
                    var level = await _dbContext.InventoryLevels.AcrossTenants()
                        .FirstOrDefaultAsync(l => l.TenantId == reservation.TenantId
                            && (variantId != null ? l.ProductVariantId == variantId : l.IngredientId == ingredientId)
                            && l.Location == null, cancellationToken);
                    if (level is not null)
                    {
                        level.Reserved -= reservation.Quantity;
                    }
                    reservation.Status = InventoryReservationStatuses.Released;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _tenantContext.TenantId = originalTenant;
            _tenantContext.ResolutionSource = originalSource;
        }

        return expired.Count;
    }

    // ── Variant-keyed wrappers (the original Spec 042 surface) ─────────────────────────────────

    public Task<decimal> GetAvailableAsync(Guid productVariantId, CancellationToken cancellationToken = default)
        => GetAvailableAsync(StockItemRef.Variant(productVariantId), cancellationToken);

    public Task SetOnHandAsync(Guid productVariantId, decimal onHand, CancellationToken cancellationToken = default)
        => SetOnHandAsync(StockItemRef.Variant(productVariantId), onHand, cancellationToken);

    // ── Internals ───────────────────────────────────────────────────────────────────────────────

    private static void ValidateKind(StockItemRef item)
    {
        if (item.Kind is not (StockItemKinds.ProductVariant or StockItemKinds.Ingredient))
        {
            throw new ArgumentException($"Unknown stock item kind '{item.Kind}'.");
        }
    }

    /// <summary>
    /// Raw-material stock addresses master data: an ingredient must exist before it can be stocked
    /// or given a reorder point (mirroring the Spec 051 costing guard). Variant levels keep the
    /// Spec 042 behaviour (no existence check) so the checkout path is unchanged.
    /// </summary>
    private async Task EnsureIngredientExistsAsync(Guid tenantId, StockItemRef item, CancellationToken cancellationToken)
    {
        if (!item.IsIngredient)
        {
            return;
        }
        var exists = await _dbContext.Ingredients.AsNoTracking()
            .AnyAsync(i => i.Id == item.Id && i.TenantId == tenantId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Ingredient '{item.Id}' was not found.");
        }
    }

    private static StockItemRef ToStockItemRef(InventoryReservation reservation)
        => reservation.ProductVariantId is { } variantId
            ? StockItemRef.Variant(variantId)
            : StockItemRef.Ingredient(reservation.IngredientId
                ?? throw new InvalidOperationException($"Reservation '{reservation.Id}' carries no stock item id."));

    private IQueryable<InventoryLevel> FindDefaultLevelQuery(Guid tenantId, StockItemRef item)
        => item.IsIngredient
            ? _dbContext.InventoryLevels.Where(l => l.TenantId == tenantId && l.IngredientId == item.Id && l.Location == null)
            : _dbContext.InventoryLevels.Where(l => l.TenantId == tenantId && l.ProductVariantId == item.Id && l.Location == null);

    private async Task<InventoryLevel> GetOrCreateDefaultLevelAsync(Guid tenantId, StockItemRef item, CancellationToken cancellationToken)
    {
        var level = await FindDefaultLevelQuery(tenantId, item).FirstOrDefaultAsync(cancellationToken);
        if (level is null)
        {
            level = new InventoryLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductVariantId = item.IsIngredient ? null : item.Id,
                IngredientId = item.IsIngredient ? item.Id : null,
                StockItemKind = item.Kind,
                Location = null,
                OnHand = 0m,
                Reserved = 0m,
            };
            _dbContext.InventoryLevels.Add(level);
        }
        return level;
    }
}
