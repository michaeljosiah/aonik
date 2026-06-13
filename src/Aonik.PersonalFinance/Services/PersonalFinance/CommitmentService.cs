using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Unified read-side service that projects <see cref="PersonalRecurringBill"/>,
/// <see cref="Subscription"/>, and <see cref="DebtRepayment"/> into a single
/// commitment view model. Also handles create-from-transaction and
/// confirm/reject workflows.
/// </summary>
internal sealed class CommitmentService : ICommitmentService
{
    /// <summary>Default reminder lead for Support commitments (Spec 044 §10); legacy Bill keeps 14.</summary>
    private const int DefaultSupportReminderDays = 3;

    private static readonly HashSet<string> RhythmUnits =
        new(StringComparer.OrdinalIgnoreCase) { "Weekly", "Monthly", "Quarterly", "Termly", "Yearly", "OneOff" };

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPaymentLogService _paymentLogService;
    private readonly ITaskService _taskService;
    private readonly ILogger<CommitmentService> _logger;

    public CommitmentService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPaymentLogService paymentLogService,
        ITaskService taskService,
        ILogger<CommitmentService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _paymentLogService = paymentLogService;
        _taskService = taskService;
        _logger = logger;
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
            throw new NotFoundException($"Transaction {request.TransactionId} not found.");

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

        throw new NotFoundException($"Commitment {commitmentId} not found.");
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

        throw new NotFoundException($"Commitment {commitmentId} not found.");
    }

    public async Task<IReadOnlyList<CommitmentItem>> ListDetectedAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = new CommitmentListFilter(VerificationStatus: "Detected", PageSize: 100);
        var result = await ListCommitmentsAsync(filter, cancellationToken);
        return result.Items;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Support commitment lifecycle (Spec 044)
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CommitmentDetail> CreateSupportAsync(
        CreateSupportCommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var entityOwned = await _dbContext.CareEntities
            .AnyAsync(e => e.Id == request.CareEntityId && e.TenantId == tenantId && e.UserId == userId, cancellationToken);
        if (!entityOwned)
        {
            throw new ArgumentException("CareEntity not found.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(request));
        }

        var unit = (request.RhythmUnit ?? "Monthly").Trim();
        if (!RhythmUnits.Contains(unit))
        {
            throw new ArgumentException($"RhythmUnit must be one of: {string.Join(", ", RhythmUnits)}.", nameof(request));
        }

        var isExplicit = unit.Equals("Termly", StringComparison.OrdinalIgnoreCase)
            || unit.Equals("OneOff", StringComparison.OrdinalIgnoreCase);
        var termDates = request.TermDates?.Select(d => d.Date).OrderBy(d => d).ToList();
        var interval = request.RhythmInterval <= 0 ? 1 : request.RhythmInterval;

        DateTime firstDue;
        if (isExplicit)
        {
            if (termDates is not { Count: > 0 })
            {
                throw new ArgumentException("Termly/OneOff commitments require explicit TermDates.", nameof(request));
            }

            firstDue = termDates[0];
        }
        else
        {
            firstDue = request.FirstDueDate.Date;
        }

        var bill = new PersonalRecurringBill
        {
            TenantId = tenantId,
            UserId = userId,
            CareEntityId = request.CareEntityId,
            Payee = request.DisplayName.Trim(),
            CommitmentKind = "Support",
            Origin = "Manual",
            VerificationStatus = "Confirmed",
            Status = "Active",
            ExpectedAmount = request.ExpectedAmount,
            Currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant(),
            RhythmUnit = unit,
            RhythmInterval = interval,
            AnchorDay = request.AnchorDay,
            TermDatesJson = termDates is { Count: > 0 } ? JsonSerializer.Serialize(termDates) : null,
            Frequency = MapRhythmToFrequency(unit, interval),
            NextDueDate = firstDue,
            ReminderDaysBefore = request.ReminderDaysBefore ?? DefaultSupportReminderDays,
            PaidFromAccountId = request.PaidFromAccountId,
            Notes = request.Notes,
            Autopay = false,
        };

        _dbContext.Set<PersonalRecurringBill>().Add(bill);
        await _dbContext.SaveChangesAsync(cancellationToken);

        OpenCycle(bill, tenantId, userId, firstDue);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ArmReminderAsync(bill, userId, cancellationToken);

        return MapBillToDetail(bill);
    }

    public async Task<CommitmentDetail?> UpdateSupportAsync(
        Guid commitmentId,
        UpdateSupportCommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var bill = await GetOwnedBillAsync(commitmentId, tenantId, userId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(request));
        }

        var unit = (request.RhythmUnit ?? "Monthly").Trim();
        if (!RhythmUnits.Contains(unit))
        {
            throw new ArgumentException($"RhythmUnit must be one of: {string.Join(", ", RhythmUnits)}.", nameof(request));
        }

        var interval = request.RhythmInterval <= 0 ? 1 : request.RhythmInterval;
        var termDates = request.TermDates?.Select(d => d.Date).OrderBy(d => d).ToList();

        // Edit never rewrites past cycles (history is append-only); it updates the
        // rhythm for future rolls and re-arms the reminder off the current due date.
        bill.Payee = request.DisplayName.Trim();
        bill.ExpectedAmount = request.ExpectedAmount;
        bill.Currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        bill.RhythmUnit = unit;
        bill.RhythmInterval = interval;
        bill.AnchorDay = request.AnchorDay;
        bill.TermDatesJson = termDates is { Count: > 0 } ? JsonSerializer.Serialize(termDates) : null;
        bill.Frequency = MapRhythmToFrequency(unit, interval);
        bill.ReminderDaysBefore = request.ReminderDaysBefore ?? bill.ReminderDaysBefore;
        bill.Notes = request.Notes;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await ReArmReminderAsync(bill, userId, cancellationToken);

        return MapBillToDetail(bill);
    }

    public async Task<CommitmentDetail?> MarkDoneAsync(
        Guid commitmentId,
        MarkCommitmentDoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var bill = await GetOwnedBillAsync(commitmentId, tenantId, userId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        if (bill.CareEntityId is not Guid careEntityId)
        {
            throw new ArgumentException("This commitment is not attached to a CareEntity; mark-done requires one.");
        }

        // Replay-safe: if this idempotency key already logged a payment, the
        // mark-done already happened — return without advancing another cycle.
        if (request.IdempotencyKey is Guid replayKey)
        {
            var alreadyLogged = await _dbContext.PaymentLogs
                .AnyAsync(p => p.TenantId == tenantId && p.UserId == userId && p.IdempotencyKey == replayKey, cancellationToken);
            if (alreadyLogged)
            {
                return MapBillToDetail(bill);
            }
        }

        var cycle = await GetCurrentCycleAsync(commitmentId, tenantId, userId, cancellationToken);
        if (cycle is null || cycle.Status == "Paid")
        {
            return MapBillToDetail(bill); // idempotent — no double log
        }

        var date = (request.Date ?? bill.NextDueDate).Date;

        // Write the PaymentLog that honours this cycle (Spec 045).
        var log = await _paymentLogService.CreateAsync(
            new CreatePaymentLogRequest(
                careEntityId,
                commitmentId,
                cycle.Id,
                request.Amount,
                request.Currency,
                request.ApproxGbp,
                date,
                request.Channel,
                "markDone",
                request.Note,
                request.IdempotencyKey),
            cancellationToken);

        cycle.Status = "Paid";
        cycle.PaymentLogId = log.Id;
        cycle.ResolvedAt = DateTime.UtcNow;

        bill.LastPaidAt = date;
        bill.LastPaidAmount = request.Amount;

        var next = RhythmFor(bill).NextAfter(cycle.DueDate);
        if (next is DateTime nextDue)
        {
            bill.NextDueDate = nextDue;
            OpenCycle(bill, tenantId, userId, nextDue);
        }
        else
        {
            bill.Status = "Completed"; // OneOff / exhausted Termly
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (next is not null)
        {
            await ReArmReminderAsync(bill, userId, cancellationToken);
        }
        else
        {
            await CancelRemindersAsync(commitmentId, cancellationToken);
        }

        return MapBillToDetail(bill);
    }

    public async Task<CommitmentDetail?> SkipCycleAsync(
        Guid commitmentId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var bill = await GetOwnedBillAsync(commitmentId, tenantId, userId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        var cycle = await GetCurrentCycleAsync(commitmentId, tenantId, userId, cancellationToken);
        if (cycle is null)
        {
            return MapBillToDetail(bill);
        }

        cycle.Status = "Skipped";
        cycle.SkipReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        cycle.ResolvedAt = DateTime.UtcNow;

        var next = RhythmFor(bill).NextAfter(cycle.DueDate);
        if (next is DateTime nextDue)
        {
            bill.NextDueDate = nextDue;
            OpenCycle(bill, tenantId, userId, nextDue);
        }
        else
        {
            bill.Status = "Completed";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (next is not null)
        {
            await ReArmReminderAsync(bill, userId, cancellationToken);
        }
        else
        {
            await CancelRemindersAsync(commitmentId, cancellationToken);
        }

        return MapBillToDetail(bill);
    }

    public async Task<CommitmentDetail?> SnoozeAsync(
        Guid commitmentId,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var bill = await GetOwnedBillAsync(commitmentId, tenantId, userId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        var cycle = await GetCurrentCycleAsync(commitmentId, tenantId, userId, cancellationToken);
        if (cycle is null)
        {
            return MapBillToDetail(bill);
        }

        // Reschedule the current cycle's reminder without resolving it (§7).
        cycle.Status = "Snoozed";
        cycle.SnoozedUntil = until;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await CancelRemindersAsync(commitmentId, cancellationToken);
        await ScheduleReminderAsync(bill, userId, DateTime.SpecifyKind(until, DateTimeKind.Utc), cancellationToken);

        return MapBillToDetail(bill);
    }

    public async Task<CommitmentDetail?> PauseAsync(Guid commitmentId, CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var bill = await GetOwnedBillAsync(commitmentId, tenantId, userId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        bill.Status = "Paused";
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var tasks = await _taskService.ListForSubjectAsync("Commitment", commitmentId, cancellationToken);
            foreach (var t in tasks)
            {
                await _taskService.PauseAsync(t.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pause reminders for commitment {CommitmentId}.", commitmentId);
        }

        return MapBillToDetail(bill);
    }

    public async Task<CommitmentDetail?> ResumeAsync(Guid commitmentId, CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var bill = await GetOwnedBillAsync(commitmentId, tenantId, userId, cancellationToken);
        if (bill is null)
        {
            return null;
        }

        bill.Status = "Active";
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ReArmReminderAsync(bill, userId, cancellationToken);

        return MapBillToDetail(bill);
    }

    public async Task<IReadOnlyList<CommitmentCycleResponse>?> GetCyclesAsync(
        Guid commitmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();
        var owned = await _dbContext.Set<PersonalRecurringBill>()
            .AnyAsync(b => b.Id == commitmentId && b.TenantId == tenantId && b.UserId == userId, cancellationToken);
        if (!owned)
        {
            return null;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var cycles = await _dbContext.Set<CommitmentCycle>()
            .AsNoTracking()
            .Where(c => c.CommitmentId == commitmentId && c.TenantId == tenantId && c.UserId == userId)
            .OrderByDescending(c => c.DueDate)
            .ThenByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return cycles.Select(MapCycle).ToList();
    }

    public async Task<int> BackfillOpenCyclesAsync(CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var active = await _dbContext.Set<PersonalRecurringBill>()
            .Where(b => b.TenantId == tenantId && b.UserId == userId && b.Status == "Active")
            .ToListAsync(cancellationToken);

        var opened = 0;
        foreach (var bill in active)
        {
            var hasOpen = await _dbContext.Set<CommitmentCycle>()
                .AnyAsync(c => c.CommitmentId == bill.Id && c.ResolvedAt == null, cancellationToken);
            if (!hasOpen)
            {
                OpenCycle(bill, tenantId, userId, bill.NextDueDate);
                opened++;
            }
        }

        if (opened > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return opened;
    }

    // ── Lifecycle helpers ───────────────────────────────────────────────

    private async Task<PersonalRecurringBill?> GetOwnedBillAsync(Guid id, Guid tenantId, Guid userId, CancellationToken ct)
        => await _dbContext.Set<PersonalRecurringBill>()
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId && b.UserId == userId, ct);

    private async Task<CommitmentCycle?> GetCurrentCycleAsync(Guid commitmentId, Guid tenantId, Guid userId, CancellationToken ct)
        => await _dbContext.Set<CommitmentCycle>()
            .Where(c => c.CommitmentId == commitmentId && c.TenantId == tenantId && c.UserId == userId && c.ResolvedAt == null)
            .OrderByDescending(c => c.DueDate)
            .FirstOrDefaultAsync(ct);

    private void OpenCycle(PersonalRecurringBill bill, Guid tenantId, Guid userId, DateTime dueDate)
        => _dbContext.Set<CommitmentCycle>().Add(new CommitmentCycle
        {
            TenantId = tenantId,
            UserId = userId,
            CommitmentId = bill.Id,
            DueDate = dueDate.Date,
            Status = "Open",
        });

    private static Rhythm RhythmFor(PersonalRecurringBill b)
        => new(b.RhythmUnit, b.RhythmInterval, b.AnchorDay, ParseTermDates(b.TermDatesJson));

    private static IReadOnlyList<DateTime>? ParseTermDates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<DateTime>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MapRhythmToFrequency(string unit, int interval)
        => unit.ToLowerInvariant() switch
        {
            "weekly" => "Weekly",
            "monthly" => "Monthly",
            "quarterly" => "Quarterly",
            "yearly" => "Annually",
            "termly" => "Quarterly",
            "oneoff" => "OneOff",
            _ => "Monthly",
        };

    private static CommitmentCycleResponse MapCycle(CommitmentCycle c)
        => new(c.Id, c.CommitmentId, c.DueDate, c.Status, c.PaymentLogId, c.SkipReason, c.SnoozedUntil, c.ResolvedAt, c.CreatedAt);

    private async Task ArmReminderAsync(PersonalRecurringBill bill, Guid userId, CancellationToken ct)
    {
        var lead = bill.ReminderDaysBefore ?? DefaultSupportReminderDays;
        var dueUtc = DateTime.SpecifyKind(bill.NextDueDate, DateTimeKind.Utc);
        await ScheduleReminderAsync(bill, userId, dueUtc.AddDays(-lead), ct);
    }

    private async Task ScheduleReminderAsync(PersonalRecurringBill bill, Guid userId, DateTime runAtUtc, CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            userId,
            severity = "Info",
            title = "Commitment due",
            body = $"{bill.Payee} is due on {bill.NextDueDate:d} ({bill.ExpectedAmount} {bill.Currency}).",
        });

        try
        {
            await _taskService.ScheduleAsync(
                new ScheduleTaskRequest(
                    Title: $"Commitment due: {bill.Payee}",
                    Kind: TaskKinds.Reminder,
                    ActionType: TaskActionTypes.NotifyUser,
                    ActionPayloadJson: payloadJson,
                    AssigneeType: TaskAssigneeTypes.System,
                    SubjectType: "Commitment",
                    SubjectId: bill.Id,
                    RunAtUtc: runAtUtc,
                    CorrelationId: bill.Id.ToString(),
                    SourceModule: "PersonalFinance"),
                ct);
        }
        catch (Exception ex)
        {
            // Reminders are a best-effort side benefit; never fail the operation.
            _logger.LogWarning(ex, "Failed to arm reminder for commitment {CommitmentId}.", bill.Id);
        }
    }

    private async Task ReArmReminderAsync(PersonalRecurringBill bill, Guid userId, CancellationToken ct)
    {
        await CancelRemindersAsync(bill.Id, ct);
        await ArmReminderAsync(bill, userId, ct);
    }

    private async Task CancelRemindersAsync(Guid commitmentId, CancellationToken ct)
    {
        try
        {
            var existing = await _taskService.ListForSubjectAsync("Commitment", commitmentId, ct);
            foreach (var t in existing)
            {
                await _taskService.CancelAsync(t.Id, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel reminders for commitment {CommitmentId}.", commitmentId);
        }
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
        UpdatedAt: b.UpdatedAt,
        CareEntityId: b.CareEntityId,
        CommitmentKind: b.CommitmentKind,
        RhythmLabel: RhythmFor(b).Label());

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
        UpdatedAt: s.UpdatedAt,
        CommitmentKind: "Subscription",
        RhythmLabel: s.Frequency);

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
        UpdatedAt: d.UpdatedAt,
        CommitmentKind: "DebtRepayment",
        RhythmLabel: d.Frequency);

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
            throw new InvalidStateException(
                $"Commitment {id} has VerificationStatus '{verificationStatus}'; expected 'Detected'.");
    }

    private static string AppendNote(string? existing, string addition)
    {
        return string.IsNullOrWhiteSpace(existing)
            ? addition
            : $"{existing}\n{addition}";
    }
}
