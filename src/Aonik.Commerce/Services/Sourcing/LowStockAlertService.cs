using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>Low-stock alerting over <see cref="CommerceDbContext"/> (Spec 052 §9/§10).</summary>
internal sealed class LowStockAlertService : ILowStockAlertService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public LowStockAlertService(CommerceDbContext dbContext, ITenantProvider tenantProvider, ITenantContext tenantContext, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<LowStockScanResult> ScanAndRaiseAsync(CancellationToken cancellationToken = default)
    {
        // Global scan — the Worker runs this without a tenant ambient (same pattern as the
        // reservation sweep): read across tenants, then write per tenant so
        // AonikDbContextBase.EnforceTenantOnWrites() sees a resolved tenant, and the outbox rows
        // enqueued alongside each raise carry the originating tenant.
        // AcrossTenants() is IgnoreQueryFilters(), which also drops the soft-delete filter — exclude deleted rows explicitly.
        var breaching = await _dbContext.InventoryLevels.AcrossTenants()
            .Where(l => !l.IsDeleted
                && l.StockItemKind == StockItemKinds.Ingredient
                && l.ReorderPoint != null
                && l.OnHand - l.Reserved <= l.ReorderPoint)
            .Join(
                _dbContext.Ingredients.AcrossTenants().Where(i => !i.IsDeleted),
                level => new { level.TenantId, Id = level.IngredientId!.Value },
                ingredient => new { ingredient.TenantId, ingredient.Id },
                (level, ingredient) => new
                {
                    level.TenantId,
                    IngredientId = ingredient.Id,
                    ingredient.Name,
                    ingredient.BaseUnit,
                    Available = level.OnHand - level.Reserved,
                    ReorderPoint = level.ReorderPoint!.Value,
                })
            .ToListAsync(cancellationToken);
        if (breaching.Count == 0)
        {
            return new LowStockScanResult(0, 0);
        }

        var raised = 0;
        var refreshed = 0;

        var originalTenant = _tenantContext.TenantId;
        var originalSource = _tenantContext.ResolutionSource;
        try
        {
            foreach (var group in breaching.GroupBy(x => x.TenantId))
            {
                _tenantContext.TenantId = group.Key;
                _tenantContext.ResolutionSource = "low-stock-scan";

                // The ACTIVE set (Open OR Acknowledged) is the one live alert per ingredient the
                // scan refreshes; only Ordered/Resolved ends the cycle (Spec 052 §9/§10). Loaded
                // up-front and kept current so a second breaching location for the same ingredient
                // in this pass refreshes the just-raised alert instead of double-raising.
                var ingredientIds = group.Select(x => x.IngredientId).Distinct().ToList();
                // AcrossTenants() also drops the soft-delete filter — a deleted alert must not be refreshed.
                var activeAlerts = await _dbContext.LowStockAlerts.AcrossTenants()
                    .Where(a => !a.IsDeleted
                        && a.TenantId == group.Key
                        && ingredientIds.Contains(a.IngredientId)
                        && (a.Status == LowStockAlertStatuses.Open || a.Status == LowStockAlertStatuses.Acknowledged))
                    .ToListAsync(cancellationToken);
                var activeByIngredient = activeAlerts.ToDictionary(a => a.IngredientId);

                foreach (var item in group)
                {
                    if (activeByIngredient.TryGetValue(item.IngredientId, out var active))
                    {
                        // IDEMPOTENT refresh: update the snapshot; never insert a second alert,
                        // never flip Acknowledged back to Open, never re-notify (Spec 052 §9).
                        active.AvailableAtRaise = item.Available;
                        active.ReorderPoint = item.ReorderPoint;
                        refreshed++;
                        continue;
                    }

                    var alert = new LowStockAlert
                    {
                        Id = Guid.NewGuid(),
                        TenantId = group.Key,
                        IngredientId = item.IngredientId,
                        AvailableAtRaise = item.Available,
                        ReorderPoint = item.ReorderPoint,
                        Status = LowStockAlertStatuses.Open,
                        RaisedAt = _clock.UtcNow,
                    };
                    _dbContext.LowStockAlerts.Add(alert);
                    activeByIngredient[item.IngredientId] = alert;

                    // NEW alerts only: surface once through the Spec 016 admin inbox via the
                    // outbox, atomically with the alert row (Spec 052 §10).
                    _dbContext.EnqueueIntegrationEvent(new LowStockAlertRaisedEvent(
                        group.Key, alert.Id, item.IngredientId, item.Name, item.BaseUnit, item.Available, item.ReorderPoint));
                    raised++;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _tenantContext.TenantId = originalTenant;
            _tenantContext.ResolutionSource = originalSource;
        }

        return new LowStockScanResult(raised, refreshed);
    }

    public async Task<IReadOnlyList<LowStockAlertDto>> ListAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.LowStockAlerts.AsNoTracking()
            .Where(a => a.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        var rows = await query
            .GroupJoin(
                _dbContext.Ingredients.AsNoTracking().Where(i => i.TenantId == tenantId),
                alert => alert.IngredientId,
                ingredient => ingredient.Id,
                (alert, ingredients) => new { alert, ingredients })
            .SelectMany(
                x => x.ingredients.DefaultIfEmpty(),
                (x, ingredient) => new { x.alert, IngredientName = ingredient != null ? ingredient.Name : null })
            .OrderByDescending(x => x.alert.RaisedAt)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => Map(x.alert, x.IngredientName))
            .ToList();
    }

    public async Task<LowStockAlertDto?> AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var alert = await _dbContext.LowStockAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.TenantId == tenantId, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        if (alert.Status == LowStockAlertStatuses.Open)
        {
            alert.Status = LowStockAlertStatuses.Acknowledged;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (alert.Status != LowStockAlertStatuses.Acknowledged)
        {
            throw new InvalidOperationException(
                $"Low-stock alert '{alertId}' is {alert.Status} and can no longer be acknowledged.");
        }

        var ingredientName = await _dbContext.Ingredients.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.Id == alert.IngredientId)
            .Select(i => (string?)i.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return Map(alert, ingredientName);
    }

    private static LowStockAlertDto Map(LowStockAlert alert, string? ingredientName)
        => new(alert.Id, alert.IngredientId, ingredientName, alert.AvailableAtRaise, alert.ReorderPoint, alert.Status, alert.RaisedAt);
}
