using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Fulfilment;

internal sealed class DeliveryReservationService(CommerceDbContext db, ITenantProvider tenantProvider, IClock clock)
    : IDeliveryReservationService
{
    public async Task<CartDeliveryReservationDto> GetAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var cart = await db.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken);
        if (cart is null || cart.BoxBundleProductId is null || !CartAccess.IsAuthorized(cart, access))
            throw new NotFoundException("Cart was not found.");
        var hold = await db.CartDeliveryReservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.CartId == cartId, cancellationToken);
        var now = clock.UtcNow;
        return new(cartId, Convert.ToBase64String(cart.RowVersion), now, hold is null ? null : new(
            hold.Id, hold.DeliveryDate,
            hold.Status == DeliveryReservationStatuses.Held && hold.ExpiresAtUtc <= now ? "Expired" : hold.Status,
            hold.SelectedAtUtc, hold.ExpiresAtUtc, hold.PaymentAttemptId,
            hold.PaymentStartedAtUtc, hold.PaymentDeadlineUtc, hold.OrderId));
    }

    public Task<CartDeliveryReservationDto> ReserveAsync(Guid cartId, DateOnly date, CartAccessContext access, CancellationToken cancellationToken = default)
        => WriteSelectionAsync(cartId, date, access, cancellationToken);

    public Task<CartDeliveryReservationDto> ReleaseAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
        => WriteSelectionAsync(cartId, null, access, cancellationToken);

    private async Task<CartDeliveryReservationDto> WriteSelectionAsync(Guid cartId, DateOnly? date, CartAccessContext access, CancellationToken ct)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        try
        {
            for (var retry = 0; ; retry++)
            {
                try
                {
                    await db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
                    {
                        Detach(cartId);
                        CartTracking.Detach(db, tenantId, cartId);
                        await using var transaction = db.Database.IsRelational()
                            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token) : null;
                        var cart = await db.Carts.SingleOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, token);
                        if (cart is null || cart.BoxBundleProductId is null || !CartAccess.IsAuthorized(cart, access))
                            throw new NotFoundException("Cart was not found.");
                        CartWriteGuard.RequireCurrent(cart, access);
                        var draft = (CartDraftData.Read(cart) ?? new CartCheckoutDraftDto()) with { DeliveryDate = date };
                        var json = CartDraftData.Serialize(draft);
                        await SelectTrackedAsync(cart, date, token);
                        if (cart.CheckoutDraftJson != json || db.ChangeTracker.HasChanges())
                        {
                            cart.CheckoutDraftJson = json;
                            CartActivity.UserEdit(db, cart, clock);
                            await db.SaveChangesAsync(token);
                        }
                        if (transaction is not null) await transaction.CommitAsync(token);
                    }, ct);
                    break;
                }
                catch (DbUpdateConcurrencyException) when (retry == 0)
                {
                    // Recount one capacity collision. The original cart-version guard still
                    // rejects a stale customer edit; it is never refreshed behind the caller.
                }
            }
        }
        catch (DbUpdateException)
        {
            throw new DeliveryReservationException(DeliveryReservationException.Conflict, "Delivery availability changed. Refresh the cart and choose again.");
        }
        finally
        {
            Detach(cartId);
            CartTracking.Detach(db, tenantId, cartId);
        }
        return await GetAsync(cartId, access, ct);
    }

    // Tracked helpers deliberately do not save or open transactions. The caller owns the cart
    // claim and saves these rows with its draft/checkout changes on this same Commerce context.
    internal async Task<CartDeliveryReservation?> SelectTrackedAsync(Cart cart, DateOnly? date, CancellationToken ct = default)
    {
        RequireBox(cart);
        if (!CartWriteGuard.IsEditable(cart)) throw Conflict();
        var hold = await LoadAsync(cart, ct);
        if (hold?.Status is DeliveryReservationStatuses.PaymentPending or DeliveryReservationStatuses.Committed) throw Conflict();
        if (date is null)
        {
            await ReleaseTrackedAsync(cart, null, ct);
            return null;
        }
        await ValidateCalendarAsync(date.Value, ct);
        var pool = await db.DeliveryDateCapacities.SingleOrDefaultAsync(p => p.TenantId == cart.TenantId && p.DeliveryDate == date, ct);
        if (pool is null) throw Unknown();
        if (pool.Unit != "box")
        {
            if (db.Entry(pool).State == EntityState.Unchanged) db.Entry(pool).State = EntityState.Detached;
            throw Unknown();
        }
        var now = clock.UtcNow;
        if (hold is { Status: DeliveryReservationStatuses.Held } && hold.DeliveryDate == date && hold.ExpiresAtUtc > now)
            return hold;
        var occupied = await Counted(db, cart.TenantId, now).CountAsync(r => r.CapacityId == pool.Id && r.CartId != cart.Id, ct);
        if (occupied >= pool.Capacity)
        {
            if (db.Entry(pool).State == EntityState.Unchanged) db.Entry(pool).State = EntityState.Detached;
            throw new DeliveryReservationException(DeliveryReservationException.Full, "This delivery date just filled. Choose another date.");
        }
        if (hold is null)
        {
            hold = new CartDeliveryReservation { TenantId = cart.TenantId, CartId = cart.Id };
            db.CartDeliveryReservations.Add(hold);
        }
        else if (hold.CapacityId != pool.Id) Touch(await PoolAsync(cart.TenantId, hold.CapacityId, ct));
        hold.CapacityId = pool.Id;
        hold.DeliveryDate = date.Value;
        hold.SelectedAtUtc = now;
        hold.ExpiresAtUtc = now.AddMinutes(15);
        hold.Status = DeliveryReservationStatuses.Held;
        hold.PaymentAttemptId = null;
        hold.PaymentStartedAtUtc = null;
        hold.PaymentDeadlineUtc = null;
        hold.OrderId = null;
        Touch(pool);
        return hold;
    }

    internal async Task<CartDeliveryReservation> BeginPaymentTrackedAsync(Cart cart, DateOnly date, Guid attemptId, CancellationToken ct = default)
    {
        RequireBox(cart);
        if (attemptId == Guid.Empty) throw Conflict();
        await ValidateCalendarAsync(date, ct);
        var hold = await LoadAsync(cart, ct) ?? throw Expired();
        if (hold.DeliveryDate != date) throw Conflict();
        var pool = await PoolAsync(cart.TenantId, hold.CapacityId, ct);
        if (pool.Unit != "box") throw Unknown();
        var now = clock.UtcNow;
        if (hold.Status == DeliveryReservationStatuses.PaymentPending)
        {
            if (hold.PaymentAttemptId != attemptId || hold.PaymentDeadlineUtc is null || now >= hold.PaymentDeadlineUtc) throw Expired();
            return hold;
        }
        if (hold.Status != DeliveryReservationStatuses.Held || hold.ExpiresAtUtc <= now) throw Expired();
        hold.Status = DeliveryReservationStatuses.PaymentPending;
        hold.PaymentAttemptId = attemptId;
        hold.PaymentStartedAtUtc = now;
        hold.PaymentDeadlineUtc = now.AddMinutes(10);
        Touch(pool);
        return hold;
    }

    internal async Task BindOrderTrackedAsync(Cart cart, Guid reservationId, Guid attemptId, Guid orderId, DateOnly date, CancellationToken ct = default)
    {
        var hold = await RequirePendingAsync(cart, reservationId, attemptId, ct);
        if (hold.DeliveryDate != date || hold.OrderId is { } existing && existing != orderId) throw Conflict();
        hold.OrderId = orderId;
    }

    internal async Task CommitTrackedAsync(Cart cart, Guid reservationId, Guid attemptId, Guid orderId, DateOnly date, CancellationToken ct = default)
    {
        var hold = await LoadAsync(cart, ct) ?? throw Conflict();
        if (hold.Id != reservationId || hold.PaymentAttemptId != attemptId || hold.OrderId != orderId || hold.DeliveryDate != date) throw Conflict();
        if (hold.Status == DeliveryReservationStatuses.Committed) return;
        if (hold.Status != DeliveryReservationStatuses.PaymentPending) throw Conflict();
        hold.Status = DeliveryReservationStatuses.Committed;
        Touch(await PoolAsync(cart.TenantId, hold.CapacityId, ct));
    }

    internal async Task ReleaseTrackedAsync(Cart cart, Guid? expectedAttemptId, CancellationToken ct = default)
    {
        var hold = await LoadAsync(cart, ct);
        if (hold is null || hold.Status == DeliveryReservationStatuses.Released) return;
        if (hold.Status == DeliveryReservationStatuses.Committed
            || hold.Status == DeliveryReservationStatuses.PaymentPending && (expectedAttemptId is null || hold.PaymentAttemptId != expectedAttemptId)
            || expectedAttemptId is not null && hold.PaymentAttemptId != expectedAttemptId) throw Conflict();
        hold.Status = DeliveryReservationStatuses.Released;
        Touch(await PoolAsync(cart.TenantId, hold.CapacityId, ct));
    }

    internal async Task<bool> ExpireHeldTrackedAsync(Cart cart, CancellationToken ct = default)
    {
        var hold = await LoadAsync(cart, ct);
        if (hold is not { Status: DeliveryReservationStatuses.Held } || hold.ExpiresAtUtc > clock.UtcNow) return false;
        await ReleaseTrackedAsync(cart, null, ct);
        return true;
    }

    internal void Detach(Guid cartId)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var holds = db.ChangeTracker.Entries<CartDeliveryReservation>()
            .Where(e => e.Entity.TenantId == tenantId && e.Entity.CartId == cartId).ToList();
        var ids = holds.Select(e => e.Entity.CapacityId).ToHashSet();
        ids.UnionWith(holds.Select(e => e.Property(r => r.CapacityId).OriginalValue));
        foreach (var entry in db.ChangeTracker.Entries<DeliveryDateCapacity>().Where(e => e.Entity.TenantId == tenantId
                     && ids.Contains(e.Entity.Id)).ToList()) entry.State = EntityState.Detached;
        foreach (var entry in holds) entry.State = EntityState.Detached;
    }

    public async Task<IReadOnlyList<DeliveryDateCapacityDto>> GetCapacitiesAsync(DateOnly fromDate, int days = 31, CancellationToken cancellationToken = default)
    {
        FulfilmentPromiseCalculator.ValidateRange(fromDate, days);
        var toDate = fromDate.AddDays(days - 1);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var rows = await db.DeliveryDateCapacities.AsNoTracking().Where(p => p.TenantId == tenantId
            && p.DeliveryDate >= fromDate && p.DeliveryDate <= toDate).OrderBy(p => p.DeliveryDate).ToListAsync(cancellationToken);
        var counts = await CountsAsync(db, tenantId, rows.Select(p => p.Id).ToList(), clock.UtcNow, cancellationToken);
        return rows.Select(p => Map(p, counts.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<DeliveryDateCapacityDto> UpdateCapacityAsync(DateOnly date, UpdateDeliveryDateCapacityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Unit != "box" || request.Capacity < 0)
            throw new StorefrontValidationException("Explicit unit 'box' and a nonnegative capacity are required. One box includes every permitted size and extra.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        Guid poolId = Guid.NewGuid();
        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                foreach (var entry in db.ChangeTracker.Entries<DeliveryDateCapacity>()
                             .Where(e => e.Entity.TenantId == tenantId && e.Entity.DeliveryDate == date).ToList()) entry.State = EntityState.Detached;
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
                var pool = await db.DeliveryDateCapacities.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.DeliveryDate == date, ct);
                if (pool is null)
                {
                    if (request.ExpectedVersion is not null) throw Conflict();
                    pool = new DeliveryDateCapacity { Id = poolId, TenantId = tenantId, DeliveryDate = date };
                    db.DeliveryDateCapacities.Add(pool);
                }
                else if (!MatchesVersion(pool.RowVersion, request.ExpectedVersion)) throw Conflict();
                var occupied = await Counted(db, tenantId, clock.UtcNow).CountAsync(r => r.CapacityId == pool.Id, ct);
                if (request.Capacity < occupied)
                    throw new DeliveryReservationException(DeliveryReservationException.Conflict, "Capacity cannot be reduced below held or committed boxes.");
                pool.Unit = request.Unit;
                pool.Capacity = request.Capacity;
                if (db.Entry(pool).State != EntityState.Added) Touch(pool);
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateException) { throw Conflict(); }
        finally
        {
            foreach (var entry in db.ChangeTracker.Entries<DeliveryDateCapacity>()
                         .Where(e => e.Entity.TenantId == tenantId && e.Entity.DeliveryDate == date).ToList()) entry.State = EntityState.Detached;
        }
        return (await GetCapacitiesAsync(date, 1, cancellationToken)).Single();
    }

    internal static async Task<IReadOnlyDictionary<DateOnly, DeliveryDateAvailabilityDto>> ReadAvailabilityAsync(
        CommerceDbContext context, Guid tenantId, IReadOnlyCollection<DateOnly> dates, DateTime now, CancellationToken ct)
    {
        var rows = await context.DeliveryDateCapacities.AsNoTracking()
            .Where(p => p.TenantId == tenantId && dates.Contains(p.DeliveryDate)).ToListAsync(ct);
        var byDate = rows.ToDictionary(p => p.DeliveryDate);
        var counts = await CountsAsync(context, tenantId, rows.Select(p => p.Id).ToList(), now, ct);
        return dates.Distinct().ToDictionary(date => date, date => new DeliveryDateAvailabilityDto(date,
            !byDate.TryGetValue(date, out var pool) || pool.Unit != "box" ? DeliveryAvailabilityStatuses.Unknown
                : counts.GetValueOrDefault(pool.Id) >= pool.Capacity ? DeliveryAvailabilityStatuses.FullyBooked : DeliveryAvailabilityStatuses.Available));
    }

    private static Task<Dictionary<Guid, int>> CountsAsync(CommerceDbContext context, Guid tenantId, List<Guid> ids, DateTime now, CancellationToken ct)
        => Counted(context, tenantId, now).Where(r => ids.Contains(r.CapacityId))
            .GroupBy(r => r.CapacityId).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Id, row => row.Count, ct);

    private static IQueryable<CartDeliveryReservation> Counted(CommerceDbContext context, Guid tenantId, DateTime now)
        => context.CartDeliveryReservations.Where(r => r.TenantId == tenantId
            && (r.Status == DeliveryReservationStatuses.Committed || r.Status == DeliveryReservationStatuses.PaymentPending
                || r.Status == DeliveryReservationStatuses.Held && r.ExpiresAtUtc > now));

    private async Task ValidateCalendarAsync(DateOnly date, CancellationToken ct)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var calendar = await db.FulfilmentCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.TenantId == tenantId, ct);
        var offered = calendar is null ? null : FulfilmentPromiseCalculator.DeliveryDates(calendar, clock.UtcNow, date, 1);
        if (offered is null) throw Unknown();
        if (!offered.Dates.Contains(date))
            throw new DeliveryReservationException(DeliveryReservationException.NoDelivery, "Delivery is not offered on this date.");
    }

    private void RequireBox(Cart cart)
    {
        if (cart.TenantId != tenantProvider.GetCurrentTenantId() || cart.BoxBundleProductId is null)
            throw new NotFoundException("Box cart was not found.");
    }

    private Task<CartDeliveryReservation?> LoadAsync(Cart cart, CancellationToken ct)
    {
        RequireBox(cart);
        return db.CartDeliveryReservations.SingleOrDefaultAsync(r => r.TenantId == cart.TenantId && r.CartId == cart.Id, ct);
    }

    private async Task<CartDeliveryReservation> RequirePendingAsync(Cart cart, Guid reservationId, Guid attemptId, CancellationToken ct)
    {
        var hold = await LoadAsync(cart, ct) ?? throw Conflict();
        if (hold.Id != reservationId || hold.PaymentAttemptId != attemptId || hold.Status != DeliveryReservationStatuses.PaymentPending) throw Conflict();
        return hold;
    }

    private async Task<DeliveryDateCapacity> PoolAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await db.DeliveryDateCapacities.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct) ?? throw Unknown();

    private void Touch(DeliveryDateCapacity pool)
    {
        pool.UpdatedAt = clock.UtcNow;
        db.Entry(pool).Property(p => p.UpdatedAt).IsModified = true;
    }

    private static bool MatchesVersion(byte[] actual, string? expected)
    {
        if (expected is null || expected.Length > 64) return false;
        try { return actual.AsSpan().SequenceEqual(Convert.FromBase64String(expected)); }
        catch (FormatException) { return false; }
    }

    private static DeliveryDateCapacityDto Map(DeliveryDateCapacity pool, int occupied)
        => new(pool.DeliveryDate, pool.Unit, pool.Capacity, occupied, Convert.ToBase64String(pool.RowVersion));
    private static DeliveryReservationException Unknown() => new(DeliveryReservationException.Unavailable, "Delivery availability is unknown. Please try again.");
    private static DeliveryReservationException Conflict() => new(DeliveryReservationException.Conflict, "The delivery reservation changed. Refresh before trying again.");
    private static DeliveryReservationException Expired() => new(DeliveryReservationException.Expired, "The delivery reservation expired. Choose a date again.");
}
