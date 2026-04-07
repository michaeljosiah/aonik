using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Unified read-side service that projects <see cref="PersonalRecurringBill"/>,
/// <see cref="Subscription"/>, and <see cref="DebtRepayment"/> into a single
/// commitment view model. Also handles create-from-transaction and
/// confirm/reject workflows.
/// </summary>
internal sealed class CommitmentService : ICommitmentService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CommitmentService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    // ═══════════════════════════════════════════════════════════════════
    // List
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CommitmentListResponse> ListCommitmentsAsync(
        CommitmentListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        // Project all three entity types into a common shape (null = type excluded by filter)
        var bills = ProjectBills(tenantId, userId, filter);
        var subs = ProjectSubscriptions(tenantId, userId, filter);
        var debts = ProjectDebtRepayments(tenantId, userId, filter);

        // Union in-memory after each source is filtered at the DB level
        var allItems = bills is not null ? await bills.ToListAsync(cancellationToken) : new List<CommitmentItem>();
        if (subs is not null) allItems.AddRange(await subs.ToListAsync(cancellationToken));
        if (debts is not null) allItems.AddRange(await debts.ToListAsync(cancellationToken));

        // Search filter (applied in-memory across DisplayName)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            allItems = allItems
                .Where(i => i.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Sort by due date ascending
        allItems = allItems.OrderBy(i => i.DueDate).ToList();

        // Totals (computed before pagination)
        var now = DateTime.UtcNow;
        var dueSoonThreshold = now.AddDays(7);
        var totals = new CommitmentTotals(
            TotalUpcomingAmount: allItems
                .Where(i => i.Status is "Active" && i.DueDate >= now)
                .Sum(i => i.Amount ?? 0),
            DueSoonCount: allItems
                .Count(i => i.Status is "Active" && i.DueDate >= now && i.DueDate <= dueSoonThreshold),
            DetectedCount: allItems.Count(i => i.VerificationStatus is "Detected"),
            BillsCount: allItems.Count(i => i.CommitmentType is "Bill"),
            SubscriptionsCount: allItems.Count(i => i.CommitmentType is "Subscription"),
            DebtRepaymentsCount: allItems.Count(i => i.CommitmentType is "DebtRepayment"));

        // Paginate
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var paged = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1) // take one extra to determine HasMore
            .ToList();

        var hasMore = paged.Count > pageSize;
        if (hasMore) paged.RemoveAt(paged.Count - 1);

        return new CommitmentListResponse(paged, page, pageSize, hasMore, totals);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Get single
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CommitmentDetail?> GetCommitmentAsync(
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        // Try each source type in turn
        var bill = await _dbContext.Set<PersonalRecurringBill>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == commitmentId && b.TenantId == tenantId && b.UserId == userId, cancellationToken);

        if (bill is not null)
            return MapBillToDetail(bill);

        var sub = await _dbContext.Set<Subscription>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == commitmentId && s.TenantId == tenantId && s.UserId == userId, cancellationToken);

        if (sub is not null)
            return MapSubscriptionToDetail(sub);

        var debt = await _dbContext.Set<DebtRepayment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == commitmentId && d.TenantId == tenantId && d.UserId == userId, cancellationToken);

        if (debt is not null)
            return MapDebtToDetail(debt);

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Create from transaction
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CommitmentDetail> CreateFromTransactionAsync(
        CreateCommitmentFromTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        // Verify the source transaction exists
        var txExists = await _dbContext.Set<PersonalTransaction>()
            .AnyAsync(t =>
                t.Id == request.TransactionId &&
                t.TenantId == tenantId &&
                t.UserId == userId, cancellationToken);

        if (!txExists)
            throw new InvalidOperationException($"Transaction {request.TransactionId} not found.");

        return request.CommitmentType switch
        {
            "Bill" => await CreateBillFromTransaction(tenantId, userId, request, cancellationToken),
            "Subscription" => await CreateSubscriptionFromTransaction(tenantId, userId, request, cancellationToken),
            "DebtRepayment" => await CreateDebtFromTransaction(tenantId, userId, request, cancellationToken),
            _ => throw new ArgumentException($"Unknown commitment type: {request.CommitmentType}")
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Confirm / Reject detected
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CommitmentDetail> ConfirmDetectedAsync(
        Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        // Try bills
        var bill = await _dbContext.Set<PersonalRecurringBill>()
            .FirstOrDefaultAsync(b => b.Id == commitmentId && b.TenantId == tenantId && b.UserId == userId, cancellationToken);

        if (bill is not null)
        {
            EnsureDetected(bill.VerificationStatus, commitmentId);
            bill.VerificationStatus = "Confirmed";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MapBillToDetail(bill);
        }

        // Try subscriptions
        var sub = await _dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(s => s.Id == commitmentId && s.TenantId == tenantId && s.UserId == userId, cancellationToken);

        if (sub is not null)
        {
            EnsureDetected(sub.VerificationStatus, commitmentId);
            sub.VerificationStatus = "Confirmed";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MapSubscriptionToDetail(sub);
        }

        // Try debt repayments
        var debt = await _dbContext.Set<DebtRepayment>()
            .FirstOrDefaultAsync(d => d.Id == commitmentId && d.TenantId == tenantId && d.UserId == userId, cancellationToken);

        if (debt is not null)
        {
            EnsureDetected(debt.VerificationStatus, commitmentId);
            debt.VerificationStatus = "Confirmed";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MapDebtToDetail(debt);
        }

        throw new InvalidOperationException($"Commitment {commitmentId} not found.");
    }

    public async Task RejectDetectedAsync(
        Guid commitmentId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        // Try bills
        var bill = await _dbContext.Set<PersonalRecurringBill>()
            .FirstOrDefaultAsync(b => b.Id == commitmentId && b.TenantId == tenantId && b.UserId == userId, cancellationToken);

        if (bill is not null)
        {
            EnsureDetected(bill.VerificationStatus, commitmentId);
            bill.VerificationStatus = "Rejected";
            if (reason is not null) bill.Notes = AppendNote(bill.Notes, $"Rejected: {reason}");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Try subscriptions
        var sub = await _dbContext.Set<Subscription>()
            .FirstOrDefaultAsync(s => s.Id == commitmentId && s.TenantId == tenantId && s.UserId == userId, cancellationToken);

        if (sub is not null)
        {
            EnsureDetected(sub.VerificationStatus, commitmentId);
            sub.VerificationStatus = "Rejected";
            if (reason is not null) sub.Notes = AppendNote(sub.Notes, $"Rejected: {reason}");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Try debt repayments
        var debt = await _dbContext.Set<DebtRepayment>()
            .FirstOrDefaultAsync(d => d.Id == commitmentId && d.TenantId == tenantId && d.UserId == userId, cancellationToken);

        if (debt is not null)
        {
            EnsureDetected(debt.VerificationStatus, commitmentId);
            debt.VerificationStatus = "Rejected";
            if (reason is not null) debt.Notes = AppendNote(debt.Notes, $"Rejected: {reason}");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Commitment {commitmentId} not found.");
    }

    public async Task<IReadOnlyList<CommitmentItem>> ListDetectedAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = new CommitmentListFilter(VerificationStatus: "Detected", PageSize: 100);
        var result = await ListCommitmentsAsync(filter, cancellationToken);
        return result.Items;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Projection queries
    // ═══════════════════════════════════════════════════════════════════

    private IQueryable<CommitmentItem>? ProjectBills(
        Guid tenantId, Guid userId, CommitmentListFilter filter)
    {
        if (filter.Type is not null && !filter.Type.Equals("Bill", StringComparison.OrdinalIgnoreCase))
            return null;

        var query = _dbContext.Set<PersonalRecurringBill>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.UserId == userId);

        query = ApplyCommonFilters(query, filter,
            statusSelector: b => b.Status,
            verificationSelector: b => b.VerificationStatus,
            dueDateSelector: b => b.NextDueDate,
            accountSelector: b => b.PaidFromAccountId);

        return query.Select(b => new CommitmentItem(
            b.Id,
            "Bill",
            b.VerificationStatus,
            b.Origin,
            b.Payee,
            b.ExpectedAmount,
            b.Currency,
            b.NextDueDate,
            b.Frequency,
            b.Status,
            b.Autopay,
            b.PaidFromAccountId,
            b.Category,
            b.ConfidenceScore,
            b.LastPaidAt,
            b.LastPaidAmount,
            b.CreatedAt));
    }

    private IQueryable<CommitmentItem>? ProjectSubscriptions(
        Guid tenantId, Guid userId, CommitmentListFilter filter)
    {
        if (filter.Type is not null && !filter.Type.Equals("Subscription", StringComparison.OrdinalIgnoreCase))
            return null;

        var query = _dbContext.Set<Subscription>()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.UserId == userId);

        query = ApplyCommonFilters(query, filter,
            statusSelector: s => s.Status,
            verificationSelector: s => s.VerificationStatus,
            dueDateSelector: s => s.RenewalDate,
            accountSelector: s => s.PaidFromAccountId);

        return query.Select(s => new CommitmentItem(
            s.Id,
            "Subscription",
            s.VerificationStatus,
            s.Origin,
            s.Merchant,
            s.ExpectedAmount,
            s.Currency,
            s.RenewalDate,
            s.Frequency,
            s.Status,
            s.Autopay,
            s.PaidFromAccountId,
            s.Category,
            s.ConfidenceScore,
            s.LastChargedAt,
            s.LastChargedAmount,
            s.CreatedAt));
    }

    private IQueryable<CommitmentItem>? ProjectDebtRepayments(
        Guid tenantId, Guid userId, CommitmentListFilter filter)
    {
        if (filter.Type is not null && !filter.Type.Equals("DebtRepayment", StringComparison.OrdinalIgnoreCase))
            return null;

        var query = _dbContext.Set<DebtRepayment>()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.UserId == userId);

        query = ApplyCommonFilters(query, filter,
            statusSelector: d => d.Status,
            verificationSelector: d => d.VerificationStatus,
            dueDateSelector: d => d.NextDueDate,
            accountSelector: d => d.PaidFromAccountId);

        return query.Select(d => new CommitmentItem(
            d.Id,
            "DebtRepayment",
            d.VerificationStatus,
            d.Origin,
            d.CreditorName,
            d.ExpectedAmount,
            d.Currency,
            d.NextDueDate,
            d.Frequency,
            d.Status,
            d.Autopay,
            d.PaidFromAccountId,
            null, // DebtRepayment has no Category
            d.ConfidenceScore,
            d.LastPaidAt,
            d.LastPaidAmount,
            d.CreatedAt));
    }

    private static IQueryable<T> ApplyCommonFilters<T>(
        IQueryable<T> query,
        CommitmentListFilter filter,
        System.Linq.Expressions.Expression<Func<T, string>> statusSelector,
        System.Linq.Expressions.Expression<Func<T, string>> verificationSelector,
        System.Linq.Expressions.Expression<Func<T, DateTime>> dueDateSelector,
        System.Linq.Expressions.Expression<Func<T, Guid?>> accountSelector)
    {
        if (filter.Status is not null)
        {
            var statusValue = filter.Status;
            var param = statusSelector.Parameters[0];
            var body = System.Linq.Expressions.Expression.Equal(
                statusSelector.Body,
                System.Linq.Expressions.Expression.Constant(statusValue));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (filter.VerificationStatus is not null)
        {
            var vsValue = filter.VerificationStatus;
            var param = verificationSelector.Parameters[0];
            var body = System.Linq.Expressions.Expression.Equal(
                verificationSelector.Body,
                System.Linq.Expressions.Expression.Constant(vsValue));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (filter.DueFrom is not null)
        {
            var from = filter.DueFrom.Value;
            var param = dueDateSelector.Parameters[0];
            var body = System.Linq.Expressions.Expression.GreaterThanOrEqual(
                dueDateSelector.Body,
                System.Linq.Expressions.Expression.Constant(from));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (filter.DueTo is not null)
        {
            var to = filter.DueTo.Value;
            var param = dueDateSelector.Parameters[0];
            var body = System.Linq.Expressions.Expression.LessThanOrEqual(
                dueDateSelector.Body,
                System.Linq.Expressions.Expression.Constant(to));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (filter.AccountId is not null)
        {
            var accountId = filter.AccountId.Value;
            var param = accountSelector.Parameters[0];
            var body = System.Linq.Expressions.Expression.Equal(
                accountSelector.Body,
                System.Linq.Expressions.Expression.Constant((Guid?)accountId, typeof(Guid?)));
            query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param));
        }

        return query;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Create helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<CommitmentDetail> CreateBillFromTransaction(
        Guid tenantId, Guid userId,
        CreateCommitmentFromTransactionRequest request,
        CancellationToken ct)
    {
        var bill = new PersonalRecurringBill
        {
            TenantId = tenantId,
            UserId = userId,
            Payee = request.DisplayName,
            Frequency = request.Frequency,
            NextDueDate = request.NextDueDate,
            ExpectedAmount = request.ExpectedAmount,
            Currency = request.Currency,
            PaidFromAccountId = request.PaidFromAccountId,
            Autopay = request.Autopay,
            Status = "Active",
            VerificationStatus = "Confirmed",
            Origin = "PromotedFromTransaction",
            SourceTransactionId = request.TransactionId,
            Notes = request.Notes,
        };

        _dbContext.Set<PersonalRecurringBill>().Add(bill);
        await _dbContext.SaveChangesAsync(ct);
        return MapBillToDetail(bill);
    }

    private async Task<CommitmentDetail> CreateSubscriptionFromTransaction(
        Guid tenantId, Guid userId,
        CreateCommitmentFromTransactionRequest request,
        CancellationToken ct)
    {
        var sub = new Subscription
        {
            TenantId = tenantId,
            UserId = userId,
            Merchant = request.DisplayName,
            RenewalDate = request.NextDueDate,
            ExpectedAmount = request.ExpectedAmount ?? 0,
            Currency = request.Currency,
            Status = "Active",
            DetectedBy = "User",
            Frequency = request.Frequency,
            PaidFromAccountId = request.PaidFromAccountId,
            Autopay = request.Autopay,
            VerificationStatus = "Confirmed",
            Origin = "PromotedFromTransaction",
            SourceTransactionId = request.TransactionId,
            Notes = request.Notes,
        };

        _dbContext.Set<Subscription>().Add(sub);
        await _dbContext.SaveChangesAsync(ct);
        return MapSubscriptionToDetail(sub);
    }

    private async Task<CommitmentDetail> CreateDebtFromTransaction(
        Guid tenantId, Guid userId,
        CreateCommitmentFromTransactionRequest request,
        CancellationToken ct)
    {
        var debt = new DebtRepayment
        {
            TenantId = tenantId,
            UserId = userId,
            CreditorName = request.DisplayName,
            DebtType = request.DebtType ?? "Other",
            NextDueDate = request.NextDueDate,
            ExpectedAmount = request.ExpectedAmount,
            Currency = request.Currency,
            Frequency = request.Frequency,
            PaidFromAccountId = request.PaidFromAccountId,
            Autopay = request.Autopay,
            Status = "Active",
            VerificationStatus = "Confirmed",
            Origin = "PromotedFromTransaction",
            SourceTransactionId = request.TransactionId,
            Notes = request.Notes,
            AccountReference = request.AccountReference,
        };

        _dbContext.Set<DebtRepayment>().Add(debt);
        await _dbContext.SaveChangesAsync(ct);
        return MapDebtToDetail(debt);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Mapping helpers
    // ═══════════════════════════════════════════════════════════════════

    private static CommitmentDetail MapBillToDetail(PersonalRecurringBill b) => new(
        CommitmentId: b.Id,
        CommitmentType: "Bill",
        VerificationStatus: b.VerificationStatus,
        Origin: b.Origin,
        DisplayName: b.Payee,
        NormalizedMerchantOrPayee: b.Payee,
        Amount: b.ExpectedAmount,
        Currency: b.Currency,
        DueDate: b.NextDueDate,
        Frequency: b.Frequency,
        Status: b.Status,
        Autopay: b.Autopay,
        PaidFromAccountId: b.PaidFromAccountId,
        Category: b.Category,
        SubCategory: b.SubCategory,
        ConfidenceScore: b.ConfidenceScore,
        DetectionSource: b.DetectionSource,
        SourceTransactionId: b.SourceTransactionId,
        LastObservedAt: b.LastObservedAt,
        LastPaidAt: b.LastPaidAt,
        LastPaidAmount: b.LastPaidAmount,
        Notes: b.Notes,
        AccountReference: b.PayeeReference,
        CreatedAt: b.CreatedAt,
        UpdatedAt: b.UpdatedAt);

    private static CommitmentDetail MapSubscriptionToDetail(Subscription s) => new(
        CommitmentId: s.Id,
        CommitmentType: "Subscription",
        VerificationStatus: s.VerificationStatus,
        Origin: s.Origin,
        DisplayName: s.Merchant,
        NormalizedMerchantOrPayee: s.Merchant,
        Amount: s.ExpectedAmount,
        Currency: s.Currency,
        DueDate: s.RenewalDate,
        Frequency: s.Frequency,
        Status: s.Status,
        Autopay: s.Autopay,
        PaidFromAccountId: s.PaidFromAccountId,
        Category: s.Category,
        SubCategory: s.SubCategory,
        ConfidenceScore: s.ConfidenceScore,
        DetectionSource: null,
        SourceTransactionId: s.SourceTransactionId,
        LastObservedAt: s.LastObservedAt,
        LastPaidAt: s.LastChargedAt,
        LastPaidAmount: s.LastChargedAmount,
        Notes: s.Notes,
        AccountReference: null,
        CreatedAt: s.CreatedAt,
        UpdatedAt: s.UpdatedAt);

    private static CommitmentDetail MapDebtToDetail(DebtRepayment d) => new(
        CommitmentId: d.Id,
        CommitmentType: "DebtRepayment",
        VerificationStatus: d.VerificationStatus,
        Origin: d.Origin,
        DisplayName: d.CreditorName,
        NormalizedMerchantOrPayee: d.CreditorName,
        Amount: d.ExpectedAmount,
        Currency: d.Currency,
        DueDate: d.NextDueDate,
        Frequency: d.Frequency,
        Status: d.Status,
        Autopay: d.Autopay,
        PaidFromAccountId: d.PaidFromAccountId,
        Category: null,
        SubCategory: null,
        ConfidenceScore: d.ConfidenceScore,
        DetectionSource: null,
        SourceTransactionId: d.SourceTransactionId,
        LastObservedAt: d.LastObservedAt,
        LastPaidAt: d.LastPaidAt,
        LastPaidAmount: d.LastPaidAmount,
        Notes: d.Notes,
        AccountReference: d.AccountReference,
        CreatedAt: d.CreatedAt,
        UpdatedAt: d.UpdatedAt);

    // ═══════════════════════════════════════════════════════════════════
    // Shared helpers
    // ═══════════════════════════════════════════════════════════════════

    private (Guid TenantId, Guid UserId) GetContext()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = _currentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("No authenticated user.");
        return (tenantId, userId);
    }

    private static void EnsureDetected(string verificationStatus, Guid id)
    {
        if (verificationStatus is not "Detected")
            throw new InvalidOperationException(
                $"Commitment {id} has VerificationStatus '{verificationStatus}'; expected 'Detected'.");
    }

    private static string AppendNote(string? existing, string addition)
    {
        return string.IsNullOrWhiteSpace(existing)
            ? addition
            : $"{existing}\n{addition}";
    }
}
