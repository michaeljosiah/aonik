using System.Globalization;

using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Background maintenance over carts, invoked by the Worker sweep.</summary>
public interface ICartMaintenanceService
{
    /// <summary>Spec 068 A6 — box sessions idle beyond <c>Commerce.Carts.AbandonAfterDays</c>
    /// (default 14) transition to Abandoned, so a single stale anonymous session cannot pin size-
    /// plan authoring (A4's currency lock counts open sessions) forever. Returns the count. When
    /// <paramref name="tenantIds"/> is given only those tenants are swept.</summary>
    Task<int> AbandonIdleBoxCartsAsync(DateTime? asOfUtc = null, IReadOnlyCollection<Guid>? tenantIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenants with at least one idle box session as of <paramref name="asOfUtc"/>. The Worker sweep
    /// narrows this list to the tenants whose Commerce module is enabled (Spec 097 §12.2) before
    /// calling <see cref="AbandonIdleBoxCartsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindTenantsWithIdleBoxCartsAsync(DateTime? asOfUtc = null, CancellationToken cancellationToken = default);
}

internal sealed class CartMaintenanceService : ICartMaintenanceService
{
    public const string AbandonAfterDaysSettingKey = "Commerce.Carts.AbandonAfterDays";
    private const int DefaultAbandonAfterDays = 14;

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ISettingProvider _settings;
    private readonly IClock _clock;

    public CartMaintenanceService(
        CommerceDbContext dbContext,
        ITenantContext tenantContext,
        ISettingProvider settings,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _settings = settings;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Guid>> FindTenantsWithIdleBoxCartsAsync(DateTime? asOfUtc = null, CancellationToken cancellationToken = default)
    {
        var cutoff = await ResolveCutoffAsync(asOfUtc, cancellationToken);
        return await IdleBoxCarts(cutoff)
            .Select(c => c.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AbandonIdleBoxCartsAsync(DateTime? asOfUtc = null, IReadOnlyCollection<Guid>? tenantIds = null, CancellationToken cancellationToken = default)
    {
        var cutoff = await ResolveCutoffAsync(asOfUtc, cancellationToken);

        // Global sweep — the Worker runs without a tenant ambient: read across tenants, write per
        // tenant (the InventoryService.ReleaseExpiredAsync pattern).
        var query = IdleBoxCarts(cutoff);
        if (tenantIds is not null)
        {
            // Spec 097 §12.2 — the Worker passes the tenants whose Commerce module is enabled.
            var scope = tenantIds.ToList();
            query = query.Where(c => scope.Contains(c.TenantId));
        }

        var idle = await query.ToListAsync(cancellationToken);
        if (idle.Count == 0)
        {
            return 0;
        }

        var originalTenant = _tenantContext.TenantId;
        var originalSource = _tenantContext.ResolutionSource;
        try
        {
            foreach (var group in idle.GroupBy(c => c.TenantId))
            {
                _tenantContext.TenantId = group.Key;
                _tenantContext.ResolutionSource = "box-cart-abandon-sweep";
                foreach (var cart in group)
                {
                    cart.Status = CartStatuses.Abandoned;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _tenantContext.TenantId = originalTenant;
            _tenantContext.ResolutionSource = originalSource;
        }

        return idle.Count;
    }

    private async Task<DateTime> ResolveCutoffAsync(DateTime? asOfUtc, CancellationToken cancellationToken)
    {
        var at = asOfUtc ?? _clock.UtcNow;
        var raw = await _settings.GetAsync(AbandonAfterDaysSettingKey, cancellationToken);
        var days = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1
            ? parsed
            : DefaultAbandonAfterDays;
        return at.AddDays(-days);
    }

    /// <summary>
    /// Only pure building sessions abandon (OrderId null): a pending-payment cart's order, reservation
    /// and payment already exist, and abandoning it would fight payment completion — its stock frees
    /// via the reservation TTL regardless. AcrossTenants() also drops the soft-delete filter, so
    /// deleted rows are excluded explicitly.
    /// </summary>
    private IQueryable<Cart> IdleBoxCarts(DateTime cutoff)
        => _dbContext.Carts.AcrossTenants()
            .Where(c => !c.IsDeleted
                && c.BoxBundleProductId != null
                && c.Status == CartStatuses.Open
                && c.OrderId == null
                && (c.UpdatedAt ?? c.CreatedAt) < cutoff);
}
