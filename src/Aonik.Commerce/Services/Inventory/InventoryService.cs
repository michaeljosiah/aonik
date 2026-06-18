using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Inventory;

/// <summary>Stock + reservation over <see cref="CommerceDbContext"/> (Spec 042 §10).</summary>
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

    public async Task<decimal> GetAvailableAsync(Guid productVariantId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var level = await _dbContext.InventoryLevels.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.ProductVariantId == productVariantId && l.Location == null, cancellationToken);
        return level is null ? 0m : level.OnHand - level.Reserved;
    }

    public async Task SetOnHandAsync(Guid productVariantId, decimal onHand, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var level = await GetOrCreateDefaultLevelAsync(tenantId, productVariantId, cancellationToken);
        level.OnHand = onHand;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReserveAsync(Guid cartId, IReadOnlyCollection<InventoryReservationLine> lines, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Aggregate by variant so the same component chosen twice is checked as a single demand.
        var demand = lines
            .GroupBy(l => l.ProductVariantId)
            .Select(g => (VariantId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var now = _clock.UtcNow;
        var expiresAt = now.Add(ReservationTtl);

        // First pass — validate every line can be satisfied (all-or-nothing).
        var levels = new Dictionary<Guid, InventoryLevel>();
        foreach (var (variantId, quantity) in demand)
        {
            var level = await GetOrCreateDefaultLevelAsync(tenantId, variantId, cancellationToken);
            var available = level.OnHand - level.Reserved;
            if (available < quantity)
            {
                throw new InsufficientStockException(variantId, quantity, available);
            }
            levels[variantId] = level;
        }

        // Second pass — apply. Nothing was persisted above (SaveChanges is here).
        foreach (var (variantId, quantity) in demand)
        {
            levels[variantId].Reserved += quantity;
            _dbContext.InventoryReservations.Add(new InventoryReservation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductVariantId = variantId,
                CartId = cartId,
                Quantity = quantity,
                Status = InventoryReservationStatuses.Held,
                ExpiresAt = expiresAt,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CommitAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var held = await _dbContext.InventoryReservations
            .Where(r => r.TenantId == tenantId && r.CartId == cartId && r.Status == InventoryReservationStatuses.Held)
            .ToListAsync(cancellationToken);

        foreach (var reservation in held)
        {
            var level = await GetOrCreateDefaultLevelAsync(tenantId, reservation.ProductVariantId, cancellationToken);
            level.OnHand -= reservation.Quantity;
            level.Reserved -= reservation.Quantity;
            reservation.Status = InventoryReservationStatuses.Committed;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var held = await _dbContext.InventoryReservations
            .Where(r => r.TenantId == tenantId && r.CartId == cartId && r.Status == InventoryReservationStatuses.Held)
            .ToListAsync(cancellationToken);

        foreach (var reservation in held)
        {
            var level = await GetOrCreateDefaultLevelAsync(tenantId, reservation.ProductVariantId, cancellationToken);
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
        var expired = await _dbContext.InventoryReservations.AcrossTenants()
            .Where(r => r.Status == InventoryReservationStatuses.Held && r.ExpiresAt <= at)
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
                    var level = await _dbContext.InventoryLevels.AcrossTenants()
                        .FirstOrDefaultAsync(l => l.TenantId == reservation.TenantId
                            && l.ProductVariantId == reservation.ProductVariantId
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

    private async Task<InventoryLevel> GetOrCreateDefaultLevelAsync(Guid tenantId, Guid productVariantId, CancellationToken cancellationToken)
    {
        var level = await _dbContext.InventoryLevels
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.ProductVariantId == productVariantId && l.Location == null, cancellationToken);
        if (level is null)
        {
            level = new InventoryLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductVariantId = productVariantId,
                Location = null,
                OnHand = 0m,
                Reserved = 0m,
            };
            _dbContext.InventoryLevels.Add(level);
        }
        return level;
    }
}
