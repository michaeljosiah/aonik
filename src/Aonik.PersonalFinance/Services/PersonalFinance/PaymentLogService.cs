using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PaymentLogService : IPaymentLogService
{
    /// <summary>Soft-deleted logs can be restored within this window (Spec 045 §8, O3).</summary>
    private const int RestoreWindowDays = 30;

    private static readonly HashSet<string> Channels =
        new(StringComparer.OrdinalIgnoreCase) { "bank", "wise", "cash", "other" };

    private static readonly HashSet<string> Origins =
        new(StringComparer.OrdinalIgnoreCase)
        { "manual", "captureImage", "captureText", "captureVoice", "markDone", "plaidDetected" };

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PaymentLogService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PaymentLogResponse> CreateAsync(
        CreatePaymentLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        await EnsureOwnedCareEntityAsync(tenantId, userId, request.CareEntityId, cancellationToken);

        if (request.CommitmentId is Guid commitmentId)
        {
            var bill = await _dbContext.Set<PersonalRecurringBill>()
                .FirstOrDefaultAsync(b => b.Id == commitmentId && b.TenantId == tenantId && b.UserId == userId, cancellationToken);
            if (bill is null)
            {
                throw new ArgumentException("Commitment not found.", nameof(request));
            }

            // The commitment and the log must agree on the care entity, else entity totals
            // and commitment history would disagree (e.g. a log against Mum pointing at Dad's
            // commitment). A commitment with no CareEntityId (a plain bill) is not constrained.
            if (bill.CareEntityId is Guid billEntityId && billEntityId != request.CareEntityId)
            {
                throw new ArgumentException("Commitment does not belong to the specified care entity.", nameof(request));
            }

            // A supplied cycle must belong to the supplied commitment.
            if (request.CommitmentCycleId is Guid cycleId)
            {
                var cycleBelongs = await _dbContext.Set<CommitmentCycle>()
                    .AnyAsync(c => c.Id == cycleId && c.CommitmentId == commitmentId && c.TenantId == tenantId, cancellationToken);
                if (!cycleBelongs)
                {
                    throw new ArgumentException("Commitment cycle does not belong to the specified commitment.", nameof(request));
                }
            }
        }
        else if (request.CommitmentCycleId is not null)
        {
            // A cycle is meaningless without the commitment that owns it.
            throw new ArgumentException("CommitmentCycleId requires CommitmentId.", nameof(request));
        }

        // Idempotent replay — return the existing log for a repeated key. Bypass the global
        // soft-delete filter (tenant/user kept explicit): the unique index on
        // (TenantId, UserId, IdempotencyKey) still holds a soft-deleted row's key, so a replay
        // after delete must find the prior log here rather than attempt a duplicate insert that
        // fails at the database. Returning it is idempotent and does not resurrect a user-deleted log.
        if (request.IdempotencyKey is Guid key)
        {
            var existing = await _dbContext.PaymentLogs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId && p.UserId == userId && p.IdempotencyKey == key,
                    cancellationToken);
            if (existing is not null)
            {
                return MapToResponse(existing);
            }
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(request));
        }

        var log = new PaymentLog
        {
            TenantId = tenantId,
            UserId = userId,
            CareEntityId = request.CareEntityId,
            CommitmentId = request.CommitmentId,
            CommitmentCycleId = request.CommitmentCycleId,
            Amount = request.Amount,
            Currency = NormalizeCurrency(request.Currency),
            ApproxGbp = request.ApproxGbp,
            Date = request.Date,
            Channel = NormalizeChannel(request.Channel),
            Origin = NormalizeOrigin(request.Origin),
            Note = Clean(request.Note),
            CorroborationStatus = "none",
            IdempotencyKey = request.IdempotencyKey,
        };

        _dbContext.PaymentLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(log);
    }

    public async Task<PaymentLogListResponse> ListAsync(
        Guid? careEntityId,
        Guid? commitmentId,
        int? year,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.PaymentLogs
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId);

        if (careEntityId is Guid entityId)
        {
            query = query.Where(p => p.CareEntityId == entityId);
        }

        if (commitmentId is Guid cid)
        {
            query = query.Where(p => p.CommitmentId == cid);
        }

        if (year is int y)
        {
            query = query.Where(p => p.Date.Year == y);
        }

        var ordered = query.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new PaymentLogListResponse(rows.Select(MapToResponse).ToList(), page, pageSize, hasMore);
    }

    public async Task<PaymentLogResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedAsync(id, cancellationToken);
        return log is null ? null : MapToResponse(log);
    }

    public async Task<PaymentLogResponse?> UpdateAsync(
        Guid id,
        UpdatePaymentLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedAsync(id, cancellationToken);
        if (log is null)
        {
            return null;
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(request));
        }

        log.Amount = request.Amount;
        log.Currency = NormalizeCurrency(request.Currency);
        log.ApproxGbp = request.ApproxGbp;
        log.Date = request.Date;
        log.Channel = NormalizeChannel(request.Channel);
        log.Note = Clean(request.Note);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(log);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedAsync(id, cancellationToken);
        if (log is null)
        {
            return false;
        }

        // The base context converts Remove() into a soft-delete (IsDeleted = true,
        // DeletedAt = now) and the global filter then hides it from every query.
        _dbContext.PaymentLogs.Remove(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PaymentLogResponse?> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        // Soft-deleted rows are hidden by the global filter — bypass it to find one.
        var log = await _dbContext.PaymentLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.Id == id && p.TenantId == tenantId && p.UserId == userId && p.IsDeleted,
                cancellationToken);

        if (log is null || log.DeletedAt is null)
        {
            return null;
        }

        if (log.DeletedAt.Value < DateTime.UtcNow.AddDays(-RestoreWindowDays))
        {
            return null; // outside the restore window
        }

        log.IsDeleted = false;
        log.DeletedAt = null;
        log.DeletedBy = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(log);
    }

    public async Task<PaymentLogResponse?> LinkTransactionAsync(
        Guid id,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var log = await GetOwnedAsync(id, cancellationToken);
        if (log is null)
        {
            return null;
        }

        var transactionOwned = await _dbContext.Set<PersonalTransaction>()
            .AnyAsync(t => t.Id == transactionId && t.TenantId == tenantId && t.UserId == userId, cancellationToken);
        if (!transactionOwned)
        {
            throw new ArgumentException("Transaction not found.", nameof(transactionId));
        }

        log.SourceTransactionId = transactionId;
        log.CorroborationStatus = "confirmed"; // a user-confirmed link (§6)

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(log);
    }

    public async Task<PaymentLogResponse?> UnlinkTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var log = await GetOwnedAsync(id, cancellationToken);
        if (log is null)
        {
            return null;
        }

        log.SourceTransactionId = null;
        log.CorroborationStatus = "none";

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(log);
    }

    public async Task<IReadOnlyList<CurrencyTotal>> GetEntityYearTotalsAsync(
        Guid careEntityId,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var query = _dbContext.PaymentLogs
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.CareEntityId == careEntityId);

        if (year is int y)
        {
            query = query.Where(p => p.Date.Year == y);
        }

        var grouped = await query
            .GroupBy(p => p.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(p => p.Amount), g.Count()))
            .ToListAsync(cancellationToken);

        return grouped.OrderBy(t => t.Currency).ToList();
    }

    public async Task<IReadOnlyList<CareEntityPaymentLogSummary>> GetRecentForEntityAsync(
        Guid careEntityId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        count = Math.Clamp(count, 1, 100);

        var rows = await _dbContext.PaymentLogs
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && p.CareEntityId == careEntityId)
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows
            .Select(p => new CareEntityPaymentLogSummary(p.Id, p.Amount, p.Currency, p.Date, p.Channel, p.CorroborationStatus))
            .ToList();
    }

    private async Task EnsureOwnedCareEntityAsync(Guid tenantId, Guid userId, Guid careEntityId, CancellationToken ct)
    {
        var owned = await _dbContext.CareEntities
            .AnyAsync(e => e.Id == careEntityId && e.TenantId == tenantId && e.UserId == userId, ct);
        if (!owned)
        {
            throw new ArgumentException("CareEntity not found.", nameof(careEntityId));
        }
    }

    private async Task<PaymentLog?> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = GetContext();
        return await _dbContext.PaymentLogs
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId && p.UserId == userId, cancellationToken);
    }

    private (Guid TenantId, Guid UserId) GetContext()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return (tenantId, userId);
    }

    private static string NormalizeCurrency(string? currency)
        => (currency ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeChannel(string? channel)
    {
        var value = (channel ?? string.Empty).Trim();
        return Channels.Contains(value) ? value.ToLowerInvariant() : "other";
    }

    private static string NormalizeOrigin(string? origin)
    {
        var value = (origin ?? string.Empty).Trim();
        return Origins.Contains(value) ? value : "manual";
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PaymentLogResponse MapToResponse(PaymentLog p)
        => new(
            p.Id,
            p.CareEntityId,
            p.CommitmentId,
            p.CommitmentCycleId,
            p.Amount,
            p.Currency,
            p.ApproxGbp,
            p.Date,
            p.Channel,
            p.Origin,
            p.Note,
            p.SourceTransactionId,
            p.CorroborationStatus,
            p.CreatedAt,
            p.UpdatedAt);
}
