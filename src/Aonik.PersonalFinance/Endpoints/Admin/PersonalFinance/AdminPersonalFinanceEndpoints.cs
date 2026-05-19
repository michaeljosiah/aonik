using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Aonik.Finance.Endpoints.Admin.PersonalFinance;

// ── List Accounts for User ─────────────────────────────────────────────

internal sealed class AdminListAccountsRequest
{
    public Guid UserId { get; set; }
    [QueryParam] public bool IncludeArchived { get; set; }
}

internal sealed class AdminListPersonalAccountsEndpoint
    : Endpoint<AdminListAccountsRequest, IReadOnlyList<PersonalAccountResponse>>
{
    private readonly PersonalFinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListPersonalAccountsEndpoint(PersonalFinanceDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/personal-finance/users/{UserId:guid}/accounts");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AdminListAccountsRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _db.PersonalAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.UserId == req.UserId);

        if (!req.IncludeArchived)
            query = query.Where(a => !a.IsArchived);

        var accounts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new PersonalAccountResponse(
                a.Id,
                a.UserId,
                a.HouseholdId,
                a.Name,
                a.AccountType,
                a.Currency,
                a.InstitutionName,
                a.ExternalReference,
                a.Status,
                a.AccountSubtype,
                a.Last4,
                a.CurrentBalance,
                a.BalanceAsOf,
                a.IsArchived,
                a.OpenedAt,
                a.ClosedAt,
                a.CreatedAt,
                a.UpdatedAt))
            .ToListAsync(ct);

        await Send.OkAsync(accounts, ct);
    }
}

// ── List Transactions for User ─────────────────────────────────────────

internal sealed class AdminListTransactionsRequest
{
    public Guid UserId { get; set; }
    [QueryParam] public Guid? PersonalAccountId { get; set; }
    [QueryParam] public string? Category { get; set; }
    [QueryParam] public string? Search { get; set; }
    [QueryParam] public DateTime? From { get; set; }
    [QueryParam] public DateTime? To { get; set; }
    [QueryParam] public int Page { get; set; } = 1;
    [QueryParam] public int PageSize { get; set; } = 50;
}

internal sealed class AdminListPersonalTransactionsEndpoint
    : Endpoint<AdminListTransactionsRequest, IReadOnlyList<PersonalTransactionResponse>>
{
    private readonly PersonalFinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListPersonalTransactionsEndpoint(PersonalFinanceDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/personal-finance/users/{UserId:guid}/transactions");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AdminListTransactionsRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _db.PersonalTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.UserId == req.UserId);

        if (req.PersonalAccountId.HasValue)
            query = query.Where(t => t.PersonalAccountId == req.PersonalAccountId);
        if (!string.IsNullOrWhiteSpace(req.Category))
            query = query.Where(t => t.Category == req.Category);
        if (!string.IsNullOrWhiteSpace(req.Search))
            query = query.Where(t =>
                (t.Merchant != null && t.Merchant.Contains(req.Search)) ||
                (t.Description != null && t.Description.Contains(req.Search)));
        if (req.From.HasValue)
            query = query.Where(t => t.OccurredAt >= req.From.Value);
        if (req.To.HasValue)
            query = query.Where(t => t.OccurredAt <= req.To.Value);

        var skip = (req.Page - 1) * req.PageSize;
        var transactions = await query
            .OrderByDescending(t => t.OccurredAt)
            .Skip(skip)
            .Take(req.PageSize)
            .Select(t => new PersonalTransactionResponse(
                t.Id,
                t.UserId,
                t.PersonalAccountId,
                t.FinancialContextId,
                t.SourceType,
                t.OccurredAt,
                t.Amount,
                t.Currency,
                t.TransactionType,
                t.Merchant,
                t.Description,
                t.Category,
                t.SubCategory,
                t.Confidence,
                t.CategorisedBy,
                t.ClassificationMethod,
                t.Notes,
                string.IsNullOrEmpty(t.TagsJson)
                    ? Array.Empty<string>()
                    : JsonSerializer.Deserialize<IReadOnlyList<string>>(t.TagsJson) ?? Array.Empty<string>(),
                t.CreatedAt,
                t.UpdatedAt))
            .ToListAsync(ct);

        await Send.OkAsync(transactions, ct);
    }
}

// ── List Budgets for User ──────────────────────────────────────────────

internal sealed class AdminListBudgetsRequest
{
    public Guid UserId { get; set; }
}

internal sealed class AdminBudgetLineItem
{
    public string Category { get; init; } = string.Empty;
    public decimal LimitAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
}

internal sealed class AdminBudgetResponse
{
    public Guid BudgetId { get; init; }
    public string PeriodType { get; init; } = string.Empty;
    public DateTime PeriodStart { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<AdminBudgetLineItem> Lines { get; init; } = Array.Empty<AdminBudgetLineItem>();
}

internal sealed class AdminListBudgetsEndpoint
    : Endpoint<AdminListBudgetsRequest, IReadOnlyList<AdminBudgetResponse>>
{
    private readonly PersonalFinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListBudgetsEndpoint(PersonalFinanceDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/personal-finance/users/{UserId:guid}/budgets");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AdminListBudgetsRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var budgets = await _db.Budgets
            .AsNoTracking()
            .Include(b => b.Lines)
            .Where(b => b.TenantId == tenantId && b.UserId == req.UserId)
            .OrderByDescending(b => b.PeriodStart)
            .ToListAsync(ct);

        var response = budgets.Select(b => new AdminBudgetResponse
        {
            BudgetId = b.Id,
            PeriodType = b.PeriodType,
            PeriodStart = b.PeriodStart,
            Status = b.Status,
            Lines = b.Lines.Select(l => new AdminBudgetLineItem
            {
                Category = l.Category,
                LimitAmount = l.LimitAmount,
                Currency = l.Currency
            }).ToList()
        }).ToList();

        await Send.OkAsync(response, ct);
    }
}

// ── List Commitments for User ──────────────────────────────────────────

internal sealed class AdminListCommitmentsRequest
{
    public Guid UserId { get; set; }
    [QueryParam] public string? Status { get; set; }
    [QueryParam] public string? Type { get; set; }
    [QueryParam] public int Page { get; set; } = 1;
    [QueryParam] public int PageSize { get; set; } = 50;
}

internal sealed class AdminListCommitmentsEndpoint
    : Endpoint<AdminListCommitmentsRequest, CommitmentListResponse>
{
    private readonly PersonalFinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListCommitmentsEndpoint(PersonalFinanceDbContext db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/personal-finance/users/{UserId:guid}/commitments");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AdminListCommitmentsRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var skip = (req.Page - 1) * req.PageSize;

        var items = new List<CommitmentItem>();

        // Bills
        if (req.Type is null or "Bill")
        {
            var bills = await _db.Bills
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.UserId == req.UserId
                    && (req.Status == null || b.Status == req.Status))
                .Select(b => new CommitmentItem(
                    b.Id, "Bill", "Confirmed", "Manual",
                    b.Payee, b.ExpectedAmount, b.Currency,
                    b.NextDueDate, b.Frequency, b.Status, b.Autopay,
                    b.PaidFromAccountId, null, null, null, null, b.CreatedAt))
                .ToListAsync(ct);
            items.AddRange(bills);
        }

        // Personal Recurring Bills
        if (req.Type is null or "PersonalRecurringBill")
        {
            var prbs = await _db.PersonalRecurringBills
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.UserId == req.UserId
                    && (req.Status == null || b.Status == req.Status))
                .Select(b => new CommitmentItem(
                    b.Id, "PersonalRecurringBill", b.VerificationStatus, b.Origin,
                    b.Payee, b.ExpectedAmount, b.Currency,
                    b.NextDueDate, b.Frequency, b.Status, b.Autopay,
                    b.PaidFromAccountId, b.Category, b.ConfidenceScore,
                    b.LastPaidAt, b.LastPaidAmount, b.CreatedAt))
                .ToListAsync(ct);
            items.AddRange(prbs);
        }

        // Subscriptions
        if (req.Type is null or "Subscription")
        {
            var subs = await _db.Subscriptions
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.UserId == req.UserId
                    && (req.Status == null || s.Status == req.Status))
                .Select(s => new CommitmentItem(
                    s.Id, "Subscription", s.VerificationStatus, s.Origin,
                    s.Merchant, s.ExpectedAmount, s.Currency,
                    s.RenewalDate, s.Frequency, s.Status, s.Autopay,
                    s.PaidFromAccountId, s.Category, s.ConfidenceScore,
                    s.LastChargedAt, s.LastChargedAmount, s.CreatedAt))
                .ToListAsync(ct);
            items.AddRange(subs);
        }

        var ordered = items.OrderBy(i => i.DueDate).ToList();
        var page = ordered.Skip(skip).Take(req.PageSize).ToList();
        var hasMore = ordered.Count > skip + req.PageSize;
        var totalUpcoming = ordered.Where(i => i.Status == "Active").Sum(i => i.Amount ?? 0);
        var dueSoon = ordered.Count(i => i.DueDate <= DateTime.UtcNow.AddDays(7) && i.Status == "Active");

        var billsCount = items.Count(i => i.CommitmentType == "Bill" || i.CommitmentType == "PersonalRecurringBill");
        var subsCount = items.Count(i => i.CommitmentType == "Subscription");

        var response = new CommitmentListResponse(
            page,
            req.Page,
            req.PageSize,
            hasMore,
            new CommitmentTotals(totalUpcoming, dueSoon, 0, billsCount, subsCount, 0));

        await Send.OkAsync(response, ct);
    }
}

// ── Bind a Party's PF data to a User ───────────────────────────────────
//
// Stop-gap admin tool for the upcoming "invite a user and map them to an
// existing customer party" workflow. Looks up the PersonalProfile for the
// given party, then rewrites every PersonalFinance row keyed by that
// profile's UserId to the supplied target UserId (or the current admin
// user if none is supplied). Idempotent — re-binding to the same user is
// a no-op. Tenant-scoped: only touches rows in the current tenant.
//
// This makes it possible to log in (or run the playground) as the bound
// user and see Seamus/Mark Keane's seeded data without having to wire up
// the full invite-and-map UX first. Once that proper workflow exists,
// this endpoint can be kept as the lower-level primitive it calls into.

internal sealed class AdminBindPersonalFinancePartyToUserRequest
{
    /// <summary>The Party whose PersonalFinance data should be re-pointed.</summary>
    public Guid PartyId { get; set; }

    /// <summary>
    /// The User that the party's PersonalFinance data should be bound to.
    /// If null, defaults to the calling admin user — convenient for "bind
    /// to me" testing flows.
    /// </summary>
    public Guid? TargetUserId { get; init; }
}

internal sealed record AdminBindPersonalFinancePartyToUserResponse(
    Guid PartyId,
    Guid PreviousUserId,
    Guid NewUserId,
    int ProfilesUpdated,
    int AccountsUpdated,
    int TransactionsUpdated,
    int RecurringBillsUpdated,
    int BillsUpdated,
    int SubscriptionsUpdated);

internal sealed class AdminBindPersonalFinancePartyToUserEndpoint
    : Endpoint<AdminBindPersonalFinancePartyToUserRequest, AdminBindPersonalFinancePartyToUserResponse>
{
    private readonly PersonalFinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly Aonik.SharedKernel.Abstractions.ICurrentUserContext _currentUserContext;

    public AdminBindPersonalFinancePartyToUserEndpoint(
        PersonalFinanceDbContext db,
        ITenantProvider tenantProvider,
        Aonik.SharedKernel.Abstractions.ICurrentUserContext currentUserContext)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/admin/personal-finance/parties/{PartyId:guid}/bind-user");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Bind a party's personal-finance data to a user";
            s.Description = "Rewrites every PersonalFinance row keyed by the synthetic UserId of the party's PersonalProfile to the supplied target UserId (defaults to the current admin user). Tenant-scoped; idempotent; useful for testing seeded personas in the playground until the proper 'invite a user and map them to a party' workflow exists.";
            s.Response(200, "Bind succeeded");
            s.Response(401, "Not authenticated");
            s.Response(404, "No PersonalProfile found for this party");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AdminBindPersonalFinancePartyToUserRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var targetUserId = req.TargetUserId
            ?? _currentUserContext.UserId
            ?? throw new InvalidOperationException(
                "TargetUserId not supplied and the calling user has no resolvable internal UserId.");

        var profile = await _db.PersonalProfiles
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PartyId == req.PartyId, ct);

        if (profile is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var previousUserId = profile.UserId;

        if (previousUserId == targetUserId)
        {
            await Send.OkAsync(new AdminBindPersonalFinancePartyToUserResponse(
                req.PartyId, previousUserId, targetUserId, 0, 0, 0, 0, 0, 0), ct);
            return;
        }

        // ExecuteUpdate keeps these as raw UPDATE statements — no entity
        // materialisation for ~1k rows per persona, and no change-tracker
        // overhead. All filters include TenantId so we never cross tenants.

        var profilesUpdated = await _db.PersonalProfiles
            .Where(p => p.TenantId == tenantId && p.UserId == previousUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UserId, targetUserId), ct);

        var accountsUpdated = await _db.PersonalAccounts
            .Where(a => a.TenantId == tenantId && a.UserId == previousUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UserId, targetUserId), ct);

        var transactionsUpdated = await _db.PersonalTransactions
            .Where(t => t.TenantId == tenantId && t.UserId == previousUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UserId, targetUserId), ct);

        var recurringBillsUpdated = await _db.PersonalRecurringBills
            .Where(b => b.TenantId == tenantId && b.UserId == previousUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UserId, targetUserId), ct);

        var billsUpdated = await _db.Bills
            .Where(b => b.TenantId == tenantId && b.UserId == previousUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UserId, targetUserId), ct);

        var subscriptionsUpdated = await _db.Subscriptions
            .Where(s => s.TenantId == tenantId && s.UserId == previousUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UserId, targetUserId), ct);

        await Send.OkAsync(new AdminBindPersonalFinancePartyToUserResponse(
            req.PartyId,
            previousUserId,
            targetUserId,
            profilesUpdated,
            accountsUpdated,
            transactionsUpdated,
            recurringBillsUpdated,
            billsUpdated,
            subscriptionsUpdated), ct);
    }
}
