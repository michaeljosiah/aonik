using System.Text.Json;

using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Promotions;

internal sealed class DiscountService(CommerceDbContext db, ITenantProvider tenantProvider, IClock clock) : IDiscountService
{
    private const int MaximumProducts = 200;

    public static string? NormalizeCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > 64 || normalized.Any(char.IsControl)) throw new DiscountException(DiscountException.Invalid);
        return normalized;
    }

    public async Task<DiscountDto> CreateAsync(CreateDiscountCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var code = NormalizeCode(command.Code) ?? throw new InvalidStateException("A discount code is required.");
        ValidateDefinition(command.Kind, command.Value, command.Currency, command.MaxRedemptions, command.ExpiresAt);
        var products = await ValidateProductsAsync(command.EligibleProductIds, null, cancellationToken);
        if (await CodeQuery(code).AnyAsync(cancellationToken)) throw new DiscountException(DiscountException.Conflict);
        var discount = new Discount
        {
            TenantId = tenantId, Code = code, Kind = command.Kind, Value = command.Value,
            Currency = command.Currency?.Trim().ToUpperInvariant(), MaxRedemptions = command.MaxRedemptions,
            ExpiresAt = command.ExpiresAt, EligibleProductIdsJson = SerializeProducts(products)
        };
        db.Discounts.Add(discount);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.Entry(discount).State = EntityState.Detached;
            if (await CodeQuery(code).AnyAsync(cancellationToken)) throw new DiscountException(DiscountException.Conflict);
            throw;
        }
        return Map(discount, 0);
    }

    public async Task<Aonik.Commerce.Contracts.Models.Catalog.PagedResult<DiscountDto>> ListAsync(ListDiscountsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || ((long)query.Page - 1) * query.PageSize > int.MaxValue)
            throw new InvalidStateException("Page must be positive and pageSize must be between 1 and 100.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        var search = NormalizeCode(query.Search);
        var discounts = db.Discounts.AsNoTracking().Where(row => row.TenantId == tenantId);
        if (query.IsActive is { } active) discounts = discounts.Where(row => row.IsActive == active);
        if (search is not null) discounts = discounts.Where(row => row.Code.ToUpper().Contains(search));
        var total = await discounts.CountAsync(cancellationToken);
        var rows = await discounts.OrderBy(row => row.Code).ThenBy(row => row.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        var ids = rows.Select(row => row.Id).ToArray();
        var reserved = await ReservedCountsAsync(ids, cancellationToken);
        return new(rows.Select(row => Map(row, reserved.GetValueOrDefault(row.Id))).ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<DiscountDto> UpdateAsync(Guid discountId, UpdateDiscountCommand command, CancellationToken cancellationToken = default)
    {
        ValidateDefinition(command.Kind, command.Value, command.Currency, command.MaxRedemptions, command.ExpiresAt);
        byte[] expected;
        try { expected = Convert.FromBase64String(command.ExpectedVersion ?? ""); }
        catch (FormatException) { throw new DiscountException(DiscountException.Conflict); }
        if (expected.Length == 0 && db.Database.IsRelational()) throw new DiscountException(DiscountException.Conflict);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                DetachDiscount(discountId);
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct) : null;
                var tenantId = tenantProvider.GetCurrentTenantId();
                var discount = await db.Discounts.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == discountId, ct)
                    ?? throw new NotFoundException("Discount was not found.");
                if (!discount.RowVersion.SequenceEqual(expected)) throw new DiscountException(DiscountException.Conflict);
                var products = await ValidateProductsAsync(command.EligibleProductIds, ReadProducts(discount), ct);
                var reserved = await ReservedCountAsync(discountId, ct);
                if (command.MaxRedemptions is { } maximum && maximum < (long)discount.TimesRedeemed + reserved)
                    throw new InvalidStateException("The usage limit cannot be lower than redeemed and reserved uses.");
                discount.Kind = command.Kind; discount.Value = command.Value; discount.IsActive = command.IsActive;
                discount.Currency = command.Currency?.Trim().ToUpperInvariant(); discount.MaxRedemptions = command.MaxRedemptions;
                discount.ExpiresAt = command.ExpiresAt; discount.EligibleProductIdsJson = SerializeProducts(products);
                Touch(discount);
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return Map(discount, reserved);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { DetachDiscount(discountId); throw new DiscountException(DiscountException.Conflict); }
        catch { DetachDiscount(discountId); throw; }
    }

    public async Task<DiscountComputation> ComputeAsync(string? code, IReadOnlyList<DiscountChargeLine> lines,
        string currency, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCode(code);
        if (normalized is null) return new(null, null, 0m, []);
        var discount = await FindCodeAsync(normalized, tracked: false, cancellationToken);
        return Evaluate(discount, normalized, lines, currency, await ReservedCountAsync(discount.Id, cancellationToken));
    }

    public async Task<Guid?> ReserveTrackedAsync(Guid cartId, Guid attemptId, string? code, IReadOnlyList<DiscountChargeLine> lines,
        string currency, DiscountComputation expected, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCode(code);
        if (normalized is null)
        {
            if (expected.DiscountId is not null || expected.Amount != 0 || expected.Allocations.Count != 0)
                throw new DiscountException(DiscountException.PriceChanged);
            return null;
        }
        var tenantId = tenantProvider.GetCurrentTenantId();
        var reservation = await db.DiscountReservations.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.CartId == cartId, cancellationToken);
        if (reservation is not null && reservation.AttemptId == attemptId)
        {
            if (reservation.DiscountId != expected.DiscountId || reservation.Status != DiscountReservationStatuses.Reserved)
                throw new DiscountException(DiscountException.Conflict);
            return reservation.Id;
        }
        if (reservation is not null && reservation.Status != DiscountReservationStatuses.Released)
            throw new DiscountException(DiscountException.Conflict);
        var discount = await FindCodeAsync(normalized, tracked: true, cancellationToken);
        try
        {
            var actual = Evaluate(discount, normalized, lines, currency, await ReservedCountAsync(discount.Id, cancellationToken));
            if (actual.DiscountId != expected.DiscountId || actual.Code != expected.Code || actual.Amount != expected.Amount
                || !actual.Allocations.SequenceEqual(expected.Allocations)) throw new DiscountException(DiscountException.PriceChanged);
            if (reservation is null)
            {
                reservation = new DiscountReservation { TenantId = tenantId, CartId = cartId };
                db.DiscountReservations.Add(reservation);
            }
            reservation.DiscountId = discount.Id; reservation.AttemptId = attemptId;
            reservation.Status = DiscountReservationStatuses.Reserved;
            Touch(discount);
            return reservation.Id;
        }
        catch
        {
            // Validation can load a candidate without changing the cart's existing reservation.
            // Do not leave that candidate cached for a later same-scope retry after an admin edit.
            if (db.Entry(discount).State == EntityState.Unchanged) db.Entry(discount).State = EntityState.Detached;
            throw;
        }
    }

    public async Task CommitTrackedAsync(Guid cartId, Guid reservationId, Guid attemptId, Guid discountId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await GetExactReservationAsync(cartId, reservationId, attemptId, cancellationToken);
        if (reservation.DiscountId != discountId) throw new DiscountException(DiscountException.Conflict);
        if (reservation.Status == DiscountReservationStatuses.Redeemed) return;
        if (reservation.Status != DiscountReservationStatuses.Reserved) throw new DiscountException(DiscountException.Conflict);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var discount = await db.Discounts.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == discountId, cancellationToken)
            ?? throw new DiscountException(DiscountException.Conflict);
        discount.TimesRedeemed = checked(discount.TimesRedeemed + 1);
        reservation.Status = DiscountReservationStatuses.Redeemed;
        Touch(discount);
    }

    public async Task ReleaseTrackedAsync(Guid cartId, Guid reservationId, Guid attemptId, CancellationToken cancellationToken = default)
    {
        var reservation = await GetExactReservationAsync(cartId, reservationId, attemptId, cancellationToken);
        if (reservation.Status == DiscountReservationStatuses.Released) return;
        if (reservation.Status != DiscountReservationStatuses.Reserved) throw new DiscountException(DiscountException.Conflict);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var discount = await db.Discounts.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == reservation.DiscountId, cancellationToken)
            ?? throw new DiscountException(DiscountException.Conflict);
        reservation.Status = DiscountReservationStatuses.Released;
        Touch(discount);
    }

    public void Detach(Guid cartId, Guid? discountId = null)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var rows = db.ChangeTracker.Entries<DiscountReservation>()
            .Where(entry => entry.Entity.TenantId == tenantId && entry.Entity.CartId == cartId).ToList();
        var ids = rows.Select(entry => entry.Entity.DiscountId).ToHashSet();
        ids.UnionWith(rows.Select(entry => entry.Property(row => row.DiscountId).OriginalValue));
        if (discountId is { } candidate) ids.Add(candidate);
        foreach (var row in rows) row.State = EntityState.Detached;
        foreach (var id in ids) DetachDiscount(id);
    }

    public async Task MarkRedeemedAsync(Guid? discountId, CancellationToken cancellationToken = default)
    {
        if (discountId is not { } id) return;
        var tenantId = tenantProvider.GetCurrentTenantId();
        var discount = await db.Discounts.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == id, cancellationToken);
        if (discount is null) return;
        discount.TimesRedeemed = checked(discount.TimesRedeemed + 1);
        await db.SaveChangesAsync(cancellationToken);
    }

    private DiscountComputation Evaluate(Discount discount, string code, IReadOnlyList<DiscountChargeLine> lines, string currency, int reserved)
    {
        try { ValidateDefinition(discount.Kind, discount.Value, discount.Currency, discount.MaxRedemptions, discount.ExpiresAt); }
        catch (InvalidStateException) { throw new DiscountException(DiscountException.Invalid); }
        if (!discount.IsActive) throw new DiscountException(DiscountException.Inactive);
        if (discount.ExpiresAt is { } expiry && expiry <= clock.UtcNow) throw new DiscountException(DiscountException.Expired);
        if (discount.MaxRedemptions is { } maximum && (long)discount.TimesRedeemed + reserved >= maximum)
            throw new DiscountException(DiscountException.AlreadyUsed);
        if (discount.Kind == DiscountKinds.FixedAmount && !string.Equals(discount.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new DiscountException(DiscountException.CurrencyMismatch);
        if (lines.Any(line => line.Index < 0 || line.Amount < 0) || lines.Select(line => line.Index).Distinct().Count() != lines.Count)
            throw new InvalidStateException("Discount lines require unique nonnegative indexes and amounts.");
        if (lines.Any(line => line.Amount != decimal.Round(line.Amount, 4)))
            throw new InvalidStateException("Discount charge lines must have at most four decimal places so recorded allocations match the order amounts.");
        var products = ReadProducts(discount)?.ToHashSet();
        var eligible = lines.Where(line => line.Kind == "Goods" && line.Amount > 0
                && line.ProductId != Guid.Empty && (products is null || products.Contains(line.ProductId)))
            .OrderBy(line => line.Index).ToArray();
        var subtotal = eligible.Sum(line => line.Amount);
        if (subtotal <= 0) throw new DiscountException(DiscountException.NotEligible);
        var amount = Math.Min(subtotal, discount.Kind == DiscountKinds.Percentage
            ? Math.Round(subtotal * discount.Value / 100m, 2, MidpointRounding.AwayFromZero) : discount.Value);
        if (amount <= 0) throw new DiscountException(DiscountException.NotEligible);
        var allocations = DiscountAllocationMath.Allocate(amount, eligible.Select(line => line.Amount).ToArray());
        return new(discount.Id, code, amount, eligible.Select((line, index) => new DiscountAllocation(line.Index, allocations[index])).ToArray());
    }

    private IQueryable<Discount> CodeQuery(string normalized)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        return db.Discounts.Where(row => row.TenantId == tenantId && row.Code.Trim().ToUpper() == normalized);
    }

    private async Task<Discount> FindCodeAsync(string normalized, bool tracked, CancellationToken ct)
    {
        var query = CodeQuery(normalized);
        if (!tracked) query = query.AsNoTracking();
        var rows = await query.Take(2).ToListAsync(ct);
        if (rows.Count != 1) throw new DiscountException(DiscountException.Invalid);
        return rows[0];
    }

    private async Task<int> ReservedCountAsync(Guid discountId, CancellationToken ct)
        => (await ReservedCountsAsync([discountId], ct)).GetValueOrDefault(discountId);

    private async Task<Dictionary<Guid, int>> ReservedCountsAsync(IReadOnlyCollection<Guid> discountIds, CancellationToken ct)
    {
        if (discountIds.Count == 0) return [];
        var tenantId = tenantProvider.GetCurrentTenantId();
        var reservations = await db.DiscountReservations.AsNoTracking()
            .Where(row => row.TenantId == tenantId && discountIds.Contains(row.DiscountId)
                && row.Status == DiscountReservationStatuses.Reserved)
            .Select(row => new { row.CartId, row.AttemptId, row.DiscountId }).ToListAsync(ct);
        var counts = reservations.GroupBy(row => row.DiscountId).ToDictionary(group => group.Key, group => group.Count());
        var backed = reservations.Select(row => (row.CartId, row.AttemptId, row.DiscountId)).ToHashSet();
        // Checkout preparations from #344 through #354 consumed usage only on payment, but
        // have no reservation ID. Keep their pending use occupied during the #355 rollout.
        // Earlier summary-only checkout already incremented TimesRedeemed at submission.
        var legacy = await db.Carts.AsNoTracking().Where(cart => cart.TenantId == tenantId && cart.Status == CartStatuses.Open
                && (cart.CheckoutState == CartCheckoutStates.Preparing || cart.CheckoutState == CartCheckoutStates.AwaitingPayment)
                && cart.CheckoutPreparationJson != null)
            .Select(cart => new { cart.Id, cart.CheckoutPreparationJson }).ToListAsync(ct);
        foreach (var cart in legacy)
        {
            var preparation = CheckoutPreparation.Read(new Cart { Id = cart.Id, CheckoutPreparationJson = cart.CheckoutPreparationJson });
            if (preparation.DiscountReservationId is null && preparation.DiscountId is { } discountId
                && discountIds.Contains(discountId) && !backed.Contains((cart.Id, preparation.AttemptId, discountId)))
                counts[discountId] = counts.GetValueOrDefault(discountId) + 1;
        }
        return counts;
    }

    private async Task<DiscountReservation> GetExactReservationAsync(Guid cartId, Guid reservationId, Guid attemptId, CancellationToken ct)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var reservation = await db.DiscountReservations.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.CartId == cartId, ct);
        if (reservation is null || reservation.Id != reservationId || reservation.AttemptId != attemptId)
            throw new DiscountException(DiscountException.Conflict);
        return reservation;
    }

    private static void ValidateDefinition(string kind, decimal value, string? currency, int? maximum, DateTime? expiry)
    {
        if (kind is not (DiscountKinds.FixedAmount or DiscountKinds.Percentage)) throw new InvalidStateException("Discount kind must be Percentage or FixedAmount.");
        if (value <= 0 || value > 999999999999999.9999m || decimal.Round(value, kind == DiscountKinds.FixedAmount ? 2 : 4) != value
            || kind == DiscountKinds.Percentage && value > 100) throw new InvalidStateException("Discount value is invalid for this kind.");
        if (kind == DiscountKinds.FixedAmount && string.IsNullOrWhiteSpace(currency)) throw new InvalidStateException("A fixed discount requires a currency.");
        if (currency is not null && (currency.Trim().Length != 3 || currency.Trim().Any(character => !char.IsAsciiLetter(character))))
            throw new InvalidStateException("Currency must be a three-letter code.");
        if (maximum is < 0) throw new InvalidStateException("The usage limit cannot be negative.");
        if (expiry is { Kind: DateTimeKind.Local }) throw new InvalidStateException("Expiry must use UTC.");
    }

    private async Task<Guid[]?> ValidateProductsAsync(IReadOnlyList<Guid>? productIds, IReadOnlyList<Guid>? previous, CancellationToken ct)
    {
        if (productIds is null) return null;
        if (productIds.Count is 0 or > MaximumProducts || productIds.Contains(Guid.Empty) || productIds.Distinct().Count() != productIds.Count)
            throw new InvalidStateException("Choose between 1 and 200 distinct eligible products, or null for all goods.");
        var added = productIds.Except(previous ?? []).ToArray();
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (await db.Products.AsNoTracking().CountAsync(row => row.TenantId == tenantId && added.Contains(row.Id), ct) != added.Length)
            throw new InvalidStateException("Eligible products must belong to the current tenant.");
        return productIds.OrderBy(id => id).ToArray();
    }

    private static Guid[]? ReadProducts(Discount discount)
    {
        if (discount.EligibleProductIdsJson is null) return null;
        try
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(discount.EligibleProductIdsJson);
            if (ids is null || ids.Length is 0 or > MaximumProducts || ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Length)
                throw new DiscountException(DiscountException.Invalid);
            return ids;
        }
        catch (JsonException) { throw new DiscountException(DiscountException.Invalid); }
    }

    private static string? SerializeProducts(Guid[]? products) => products is null ? null : JsonSerializer.Serialize(products);
    private static DiscountDto Map(Discount discount, int reserved) => new(discount.Id, discount.Code, discount.Kind,
        discount.Value, discount.Currency, discount.IsActive, discount.MaxRedemptions, discount.TimesRedeemed,
        discount.ExpiresAt, ReadProducts(discount), reserved, Convert.ToBase64String(discount.RowVersion));
    private void Touch(Discount discount)
    {
        discount.UpdatedAt = clock.UtcNow;
        db.Entry(discount).Property(row => row.UpdatedAt).IsModified = true;
    }
    private void DetachDiscount(Guid id)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        foreach (var entry in db.ChangeTracker.Entries<Discount>().Where(entry => entry.Entity.TenantId == tenantId && entry.Entity.Id == id).ToList())
            entry.State = EntityState.Detached;
    }
}
