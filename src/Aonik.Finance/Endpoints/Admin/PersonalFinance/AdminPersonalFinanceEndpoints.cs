using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
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
    private readonly FinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListPersonalAccountsEndpoint(FinanceDbContext db, ITenantProvider tenantProvider)
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
    private readonly FinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListPersonalTransactionsEndpoint(FinanceDbContext db, ITenantProvider tenantProvider)
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
    private readonly FinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListBudgetsEndpoint(FinanceDbContext db, ITenantProvider tenantProvider)
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
    private readonly FinanceDbContext _db;
    private readonly ITenantProvider _tenantProvider;

    public AdminListCommitmentsEndpoint(FinanceDbContext db, ITenantProvider tenantProvider)
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
