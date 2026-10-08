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
    /// <summary>Empty box sessions expire after 24 hours of inactivity; sessions containing
    /// dishes expire after 7 days. Both global housekeeping windows are configurable. Returns
    /// the number actually abandoned, excluding concurrent edits and pending checkouts. When
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
    public const string AbandonAfterDaysSettingKey = CommerceSettingNames.CartsAbandonAfterDays;
    public const string EmptyAbandonAfterHoursSettingKey = CommerceSettingNames.CartsEmptyAbandonAfterHours;
    private const int DefaultAbandonAfterDays = 7;
    private const int DefaultEmptyAbandonAfterHours = 24;

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
        var cutoffs = await ResolveCutoffsAsync(asOfUtc, cancellationToken);
        return await IdleBoxCarts(cutoffs.Empty, cutoffs.Populated).AsNoTracking()
            .Select(c => c.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AbandonIdleBoxCartsAsync(DateTime? asOfUtc = null, IReadOnlyCollection<Guid>? tenantIds = null, CancellationToken cancellationToken = default)
    {
        var cutoffs = await ResolveCutoffsAsync(asOfUtc, cancellationToken);

        // Global sweep — the Worker runs without a tenant ambient: read across tenants, write per
        // tenant (the InventoryService.ReleaseExpiredAsync pattern).
        var query = IdleBoxCarts(cutoffs.Empty, cutoffs.Populated).AsNoTracking();
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
        var abandoned = 0;
        try
        {
            foreach (var cart in idle)
            {
                _tenantContext.TenantId = cart.TenantId;
                _tenantContext.ResolutionSource = "box-cart-abandon-sweep";
                CartTracking.Detach(_dbContext, cart.TenantId, cart.Id);
                _dbContext.Carts.Attach(cart);
                try
                {
                    cart.Status = CartStatuses.Abandoned;
                    CartActivity.ServerEdit(_dbContext, cart, _clock);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    abandoned++;
                }
                catch (DbUpdateConcurrencyException)
                {
                    // A customer edit or checkout won. Never reapply abandonment to its fresh
                    // version; leave it for a later sweep and continue with other idle carts.
                }
                finally
                {
                    CartTracking.Detach(_dbContext, cart.TenantId, cart.Id);
                }
            }
        }
        finally
        {
            _tenantContext.TenantId = originalTenant;
            _tenantContext.ResolutionSource = originalSource;
        }

        return abandoned;
    }

    private async Task<(DateTime Empty, DateTime Populated)> ResolveCutoffsAsync(DateTime? asOfUtc, CancellationToken cancellationToken)
    {
        var at = asOfUtc ?? _clock.UtcNow;
        var days = ReadWindow(await _settings.GetAsync(AbandonAfterDaysSettingKey, cancellationToken),
            DefaultAbandonAfterDays, 365);
        var hours = ReadWindow(await _settings.GetAsync(EmptyAbandonAfterHoursSettingKey, cancellationToken),
            DefaultEmptyAbandonAfterHours, 365 * 24);
        return (at.AddHours(-hours), at.AddDays(-days));
    }

    private static int ReadWindow(string? raw, int fallback, int maximum)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 1 && parsed <= maximum ? parsed : fallback;

    /// <summary>
    /// Pending/unknown payment results must resolve before their carts can expire. AcrossTenants
    /// drops every query filter, so child tenant ownership and both soft deletes are explicit.
    /// Extras alone do not populate a box; unavailable retained dishes still do.
    /// </summary>
    private IQueryable<Cart> IdleBoxCarts(DateTime emptyCutoff, DateTime populatedCutoff)
    {
        var items = _dbContext.CartItems.AcrossTenants();
        return _dbContext.Carts.AcrossTenants()
            .Where(c => !c.IsDeleted
                && c.BoxBundleProductId != null
                && c.Status == CartStatuses.Open
                && c.OrderId == null
                && (c.LastActivityAtUtc ?? c.UpdatedAt ?? c.CreatedAt) <=
                    (items.Any(item => item.CartId == c.Id
                        && item.TenantId == c.TenantId && !item.IsDeleted
                        && item.LineKind == CartLineKinds.BoxDish && item.BoxBundleSlotId != null
                        && item.Quantity > 0) ? populatedCutoff : emptyCutoff));
    }
}
