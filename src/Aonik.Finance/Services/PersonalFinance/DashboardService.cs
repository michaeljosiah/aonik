using System.Globalization;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Aggregates dashboard data for the Payabo mobile home screen.
/// Runs parallel queries for metrics, bills, orders, and overview,
/// then composes a single response with both raw values and formatted labels.
///
/// See docs/DashboardMetrics.md for calculation methodology.
/// </summary>
internal sealed class DashboardService : IDashboardService
{
    private const int UpcomingBillsDaysAhead = 30;
    private const int RecentOrdersLimit = 5;
    private const int UpcomingBillsLimit = 10;

    /// <summary>
    /// Account types classified as assets for net worth calculation.
    /// </summary>
    private static readonly HashSet<string> AssetAccountTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Checking", "Savings", "Investment", "Brokerage", "Retirement",
        "Cash", "MoneyMarket", "CD", "Prepaid"
    };

    /// <summary>
    /// Account types classified as liabilities for net worth calculation.
    /// </summary>
    private static readonly HashSet<string> LiabilityAccountTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreditCard", "Loan", "Mortgage", "LineOfCredit", "StudentLoan", "AutoLoan"
    };

    /// <summary>
    /// Liquid account types used for Available-to-Spend calculation.
    /// Investment/retirement accounts are excluded — they are not day-to-day spending money.
    /// </summary>
    private static readonly HashSet<string> LiquidAccountTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Checking", "Savings", "Cash", "MoneyMarket", "Prepaid"
    };

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public DashboardService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<DashboardResponse> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        // ── Run queries sequentially (DbContext is not thread-safe) ──
        var accounts = await GetActiveAccountsAsync(tenantId, userId, cancellationToken);
        var upcomingBills = await GetUpcomingBillsAsync(tenantId, userId, now, cancellationToken);
        var upcomingPersonalRecurringBills = await GetUpcomingPersonalRecurringBillsAsync(tenantId, userId, now, cancellationToken);
        var upcomingDebtRepayments = await GetUpcomingDebtRepaymentsAsync(tenantId, userId, now, cancellationToken);
        var transactions = await GetMonthTransactionsAsync(tenantId, userId, monthStart, monthEnd, cancellationToken);
        var recentOrders = await GetRecentOrdersAsync(tenantId, userId, cancellationToken);

        // ── Determine primary currency from accounts ────────────────
        var currency = DeterminePrimaryCurrency(accounts);

        // ── Build metrics (include all obligation types) ────────────
        var allUpcomingObligationAmounts = upcomingBills
            .Where(b => b.ExpectedAmount.HasValue)
            .Sum(b => b.ExpectedAmount!.Value)
            + upcomingPersonalRecurringBills
                .Where(b => b.ExpectedAmount.HasValue)
                .Sum(b => b.ExpectedAmount!.Value)
            + upcomingDebtRepayments
                .Where(d => d.ExpectedAmount.HasValue)
                .Sum(d => d.ExpectedAmount!.Value);
        var totalObligationCount = upcomingBills.Count + upcomingPersonalRecurringBills.Count + upcomingDebtRepayments.Count;
        var metrics = BuildMetrics(accounts, allUpcomingObligationAmounts, totalObligationCount, transactions, currency);

        // ── Build overview slices ───────────────────────────────────
        var overview = BuildOverview(transactions, currency, now);

        // ── Map all upcoming obligations to bill DTOs ────────────────
        var billDtos = upcomingBills
            .Select(bill => new DashboardBillDto(
                bill.Id,
                bill.Payee,
                bill.ExpectedAmount,
                FormatAmount(bill.ExpectedAmount ?? 0, bill.Currency),
                bill.Currency,
                bill.NextDueDate,
                FormatDate(bill.NextDueDate)))
            .Concat(upcomingPersonalRecurringBills
                .Select(b => new DashboardBillDto(
                    b.Id,
                    b.Payee,
                    b.ExpectedAmount,
                    FormatAmount(b.ExpectedAmount ?? 0, b.Currency),
                    b.Currency,
                    b.NextDueDate,
                    FormatDate(b.NextDueDate))))
            .Concat(upcomingDebtRepayments
                .Select(d => new DashboardBillDto(
                    d.Id,
                    d.CreditorName,
                    d.ExpectedAmount,
                    FormatAmount(d.ExpectedAmount ?? 0, d.Currency),
                    d.Currency,
                    d.NextDueDate,
                    FormatDate(d.NextDueDate))))
            .OrderBy(b => b.NextDueDate)
            .Take(UpcomingBillsLimit)
            .ToList();

        // ── Map orders to DTOs ──────────────────────────────────────
        var orderDtos = recentOrders;

        return new DashboardResponse(metrics, billDtos, orderDtos, overview);
    }

    // ═════════════════════════════════════════════════════════════════
    // Metrics Calculation
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// <summary>
    /// Builds the dashboard metrics from account balances, upcoming obligations,
    /// and this month's transactions.
    ///
    /// Calculations:
    ///   Net Worth = Sum(asset balances) - Sum(liability balances)
    ///   Total Assets = Sum of balances where AccountType is an asset type
    ///   Total Bills Due = Sum of ExpectedAmount for all upcoming obligations (bills + personal recurring bills + debt repayments)
    ///   Liquid Assets = Sum of balances for Checking / Savings / Cash / MoneyMarket / Prepaid accounts
    ///   Available to Spend = Liquid Assets - Total Bills Due  (floor 0)
    ///   Spendable Progress = Available to Spend / Liquid Assets (clamped 0..1)
    /// </summary>
    private DashboardMetricsDto BuildMetrics(
        List<PersonalAccount> accounts,
        decimal totalObligationsDue,
        int upcomingObligationsCount,
        List<PersonalTransaction> transactions,
        string currency)
    {
        // ── Net worth ───────────────────────────────────────────────
        // Assets contribute positive balance; liabilities contribute negative.
        // Accounts not matching either set are treated as assets (conservative).
        var totalAssets = 0m;
        var totalLiabilities = 0m;

        foreach (var account in accounts)
        {
            if (LiabilityAccountTypes.Contains(account.AccountType))
            {
                totalLiabilities += Math.Abs(account.CurrentBalance);
            }
            else
            {
                totalAssets += account.CurrentBalance;
            }
        }

        var netWorth = totalAssets - totalLiabilities;

        // ── Net worth change (month-over-month) ─────────────────────
        // For V1 we approximate: net change = net income this month.
        // A proper implementation would store monthly balance snapshots.
        var monthlyIncome = transactions
            .Where(t => IsIncome(t))
            .Sum(t => Math.Abs(t.Amount));

        var monthlyExpenses = transactions
            .Where(t => IsExpense(t))
            .Sum(t => Math.Abs(t.Amount));

        var netChange = monthlyIncome - monthlyExpenses;

        // Trend as percentage of net worth (guard against zero)
        var trendPercent = netWorth != 0
            ? Math.Round((double)(netChange / netWorth) * 100, 1)
            : 0.0;
        var trendDirection = netChange >= 0 ? "up" : "down";

        // ── Upcoming obligations total ───────────────────────────────
        var totalBillsDue = totalObligationsDue;

        // ── Available to spend ──────────────────────────────────────
        // Based on actual liquid account balances minus upcoming obligations.
        // This gives a meaningful number regardless of where we are in the month —
        // an income-minus-expenses formula produces £0 on the 2nd of the month
        // even when the user has £26k in their accounts.
        //
        // Formula: Liquid Assets (Checking + Savings + Cash) - Upcoming Obligations (30 days)
        //
        // Note: amounts are summed in the user's primary currency (DeterminePrimaryCurrency).
        // Cross-currency FX normalisation is deferred to V2 once exchange rate data is available.
        var liquidAssets = accounts
            .Where(a => LiquidAccountTypes.Contains(a.AccountType))
            .Sum(a => a.CurrentBalance);

        var availableToSpend = Math.Max(0, liquidAssets - totalBillsDue);

        // ── Spendable progress ──────────────────────────────────────
        // Proportion of liquid assets remaining after committed obligations.
        // 1.0 = no upcoming bills, 0.0 = obligations consume all liquid cash.
        var spendableProgress = liquidAssets > 0
            ? Math.Min(1.0, Math.Max(0.0, (double)(availableToSpend / liquidAssets)))
            : 0.0;
        var progressPercent = (int)Math.Round(spendableProgress * 100);

        return new DashboardMetricsDto(
            AvailableToSpend: availableToSpend,
            AvailableToSpendLabel: FormatAmount(availableToSpend, currency),
            AvailableToSpendSubtitle: BuildSpendableSubtitle(liquidAssets, totalBillsDue, currency),
            SpendableProgress: spendableProgress,
            SpendableProgressLabel: $"{progressPercent}% free",
            NetWorth: netWorth,
            NetWorthLabel: FormatAmount(netWorth, currency),
            NetWorthChange: netChange,
            NetWorthChangeLabel: FormatSignedAmount(netChange, currency),
            NetWorthTrendLabel: $"{trendDirection} {Math.Abs(trendPercent)}%",
            TotalAssets: totalAssets,
            AssetsLabel: FormatCompactAmount(totalAssets, currency),
            TotalBillsDue: totalBillsDue,
            BillsLabel: FormatCompactAmount(totalBillsDue, currency),
            Currency: currency,
            UpcomingBillsCount: upcomingObligationsCount);
    }

    // ═════════════════════════════════════════════════════════════════
    // Overview (Income / Expenses / Investments donut)
    // ═════════════════════════════════════════════════════════════════

    private DashboardOverviewDto BuildOverview(
        List<PersonalTransaction> transactions,
        string currency,
        DateTime now)
    {
        var income = transactions.Where(IsIncome).Sum(t => Math.Abs(t.Amount));
        var expenses = transactions.Where(IsExpense).Sum(t => Math.Abs(t.Amount));

        // Investment transactions are a subset of expenses categorized as investments.
        var investments = transactions
            .Where(t => IsExpense(t) && IsInvestmentCategory(t.Category))
            .Sum(t => Math.Abs(t.Amount));

        // Reduce expenses by investments to avoid double-counting
        var nonInvestmentExpenses = expenses - investments;

        var slices = new List<DashboardOverviewSliceDto>();

        if (income > 0)
            slices.Add(new DashboardOverviewSliceDto("Income", income, FormatAmount(income, currency), "success"));

        if (nonInvestmentExpenses > 0)
            slices.Add(new DashboardOverviewSliceDto("Expenses", nonInvestmentExpenses, FormatAmount(nonInvestmentExpenses, currency), "primary"));

        if (investments > 0)
            slices.Add(new DashboardOverviewSliceDto("Investments", investments, FormatAmount(investments, currency), "info"));

        return new DashboardOverviewDto(
            MonthLabel: now.ToString("MMMM", CultureInfo.InvariantCulture),
            MonthShortLabel: now.ToString("MMM", CultureInfo.InvariantCulture),
            YearLabel: now.Year.ToString(CultureInfo.InvariantCulture),
            Slices: slices);
    }

    // ═════════════════════════════════════════════════════════════════
    // Data Queries
    // ═════════════════════════════════════════════════════════════════

    private async Task<List<PersonalAccount>> GetActiveAccountsAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        return await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.UserId == userId && !a.IsArchived)
            .ToListAsync(ct);
    }

    private async Task<List<Bill>> GetUpcomingBillsAsync(
        Guid tenantId, Guid userId, DateTime now, CancellationToken ct)
    {
        var cutoff = now.Date.AddDays(UpcomingBillsDaysAhead);

        return await _financeDbContext.Bills
            .AsNoTracking()
            .Where(b =>
                b.TenantId == tenantId
                && b.UserId == userId
                && b.Status == "Active"
                && b.NextDueDate >= now.Date
                && b.NextDueDate <= cutoff)
            .OrderBy(b => b.NextDueDate)
            .ToListAsync(ct);
    }

    private async Task<List<PersonalRecurringBill>> GetUpcomingPersonalRecurringBillsAsync(
        Guid tenantId, Guid userId, DateTime now, CancellationToken ct)
    {
        var cutoff = now.Date.AddDays(UpcomingBillsDaysAhead);

        return await _financeDbContext.PersonalRecurringBills
            .AsNoTracking()
            .Where(b =>
                b.TenantId == tenantId
                && b.UserId == userId
                && b.Status == "Active"
                && b.VerificationStatus != "Rejected"
                && b.NextDueDate >= now.Date
                && b.NextDueDate <= cutoff)
            .OrderBy(b => b.NextDueDate)
            .ToListAsync(ct);
    }

    private async Task<List<DebtRepayment>> GetUpcomingDebtRepaymentsAsync(
        Guid tenantId, Guid userId, DateTime now, CancellationToken ct)
    {
        var cutoff = now.Date.AddDays(UpcomingBillsDaysAhead);

        return await _financeDbContext.DebtRepayments
            .AsNoTracking()
            .Where(d =>
                d.TenantId == tenantId
                && d.UserId == userId
                && d.Status == "Active"
                && d.VerificationStatus != "Rejected"
                && d.NextDueDate >= now.Date
                && d.NextDueDate <= cutoff)
            .OrderBy(d => d.NextDueDate)
            .ToListAsync(ct);
    }

    private async Task<List<PersonalTransaction>> GetMonthTransactionsAsync(
        Guid tenantId, Guid userId, DateTime monthStart, DateTime monthEnd, CancellationToken ct)
    {
        return await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(t =>
                t.TenantId == tenantId
                && t.UserId == userId
                && t.OccurredAt >= monthStart
                && t.OccurredAt <= monthEnd)
            .ToListAsync(ct);
    }

    private async Task<List<DashboardOrderDto>> GetRecentOrdersAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        // Resolve the user's PartyId so we can find their orders
        var profile = await _financeDbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.UserId == userId, ct);

        if (profile == null)
            return [];

        var partyId = profile.PartyId;

        // Find orders where the user is the Payer
        var orderIds = await _financeDbContext.Set<OrderPartyRole>()
            .AsNoTracking()
            .Where(pr =>
                pr.TenantId == tenantId
                && pr.PartyId == partyId
                && pr.Role == Entities.Orders.OrderPartyRoles.Payer)
            .Select(pr => pr.OrderId)
            .Distinct()
            .ToListAsync(ct);

        if (orderIds.Count == 0)
            return [];

        // Load the orders with their party roles to resolve beneficiary names
        var orders = await _financeDbContext.Orders
            .AsNoTracking()
            .Include(o => o.PartyRoles)
            .Where(o => orderIds.Contains(o.Id))
            .OrderByDescending(o => o.CreatedAt)
            .Take(RecentOrdersLimit)
            .ToListAsync(ct);

        // Resolve party names for receivers/payees
        var receiverPartyIds = orders
            .SelectMany(o => o.PartyRoles)
            .Where(pr => pr.Role is Entities.Orders.OrderPartyRoles.Receiver or Entities.Orders.OrderPartyRoles.Payee)
            .Select(pr => pr.PartyId)
            .Distinct()
            .ToList();

        var partyNames = receiverPartyIds.Count > 0
            ? await _financeDbContext.Parties
                .AsNoTracking()
                .Where(p => receiverPartyIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct)
            : new Dictionary<Guid, string>();

        return orders.Select(order =>
        {
            var receiverRole = order.PartyRoles
                .FirstOrDefault(pr => pr.Role is Entities.Orders.OrderPartyRoles.Receiver or Entities.Orders.OrderPartyRoles.Payee);

            var beneficiaryName = "Unknown";
            if (receiverRole != null && partyNames.TryGetValue(receiverRole.PartyId, out var name))
            {
                beneficiaryName = name;
            }

            return new DashboardOrderDto(
                order.Id,
                beneficiaryName,
                BeneficiaryPhotoUrl: null, // Photo resolution deferred to V2
                order.AmountIn,
                FormatAmount(order.AmountIn, order.CurrencyIn),
                order.OrderType,
                order.Status,
                FormatDate(order.CreatedAt));
        }).ToList();
    }

    // ═════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    /// <summary>
    /// Determines the user's primary currency from their accounts.
    /// Uses the most common currency across active accounts, falling back to GBP.
    /// </summary>
    private static string DeterminePrimaryCurrency(List<PersonalAccount> accounts)
    {
        if (accounts.Count == 0)
            return "GBP";

        return accounts
            .GroupBy(a => a.Currency.ToUpperInvariant())
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }

    private static bool IsIncome(PersonalTransaction t)
    {
        if (!string.IsNullOrWhiteSpace(t.TransactionType))
            return t.TransactionType == TransactionCategoryReference.TypeIncome;
        return t.Amount > 0;
    }

    private static bool IsExpense(PersonalTransaction t)
    {
        if (!string.IsNullOrWhiteSpace(t.TransactionType))
            return t.TransactionType == TransactionCategoryReference.TypeExpense;
        return t.Amount < 0;
    }

    private static bool IsInvestmentCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        return category.Contains("Investment", StringComparison.OrdinalIgnoreCase)
            || category.Contains("Retirement", StringComparison.OrdinalIgnoreCase)
            || category.Contains("Brokerage", StringComparison.OrdinalIgnoreCase);
    }

    // ── Currency formatting ─────────────────────────────────────────

    private static readonly Dictionary<string, string> CurrencySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GBP"] = "£",
        ["USD"] = "$",
        ["EUR"] = "€",
        ["GHS"] = "GHS ",
        ["NGN"] = "₦",
        ["KES"] = "KES ",
        ["ZAR"] = "R",
        ["CAD"] = "CA$",
        ["AUD"] = "A$",
    };

    private static string GetCurrencySymbol(string currency)
    {
        return CurrencySymbols.TryGetValue(currency, out var symbol)
            ? symbol
            : $"{currency} ";
    }

    private static string FormatAmount(decimal amount, string currency)
    {
        var symbol = GetCurrencySymbol(currency);
        return $"{symbol}{Math.Abs(amount):N2}";
    }

    private static string FormatSignedAmount(decimal amount, string currency)
    {
        var symbol = GetCurrencySymbol(currency);
        var sign = amount >= 0 ? "+" : "-";
        return $"{sign}{symbol}{Math.Abs(amount):N2}";
    }

    private static string FormatCompactAmount(decimal amount, string currency)
    {
        var symbol = GetCurrencySymbol(currency);
        var abs = Math.Abs(amount);

        if (abs >= 1_000_000)
            return $"{symbol}{abs / 1_000_000:N1}m";
        if (abs >= 1_000)
            return $"{symbol}{abs / 1_000:N1}k";

        return $"{symbol}{abs:N2}";
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("d MMM", CultureInfo.InvariantCulture);
    }

    private static string BuildSpendableSubtitle(decimal liquidAssets, decimal totalBillsDue, string currency)
    {
        if (liquidAssets == 0)
            return "No liquid accounts linked yet.";

        if (totalBillsDue == 0)
            return $"{FormatAmount(liquidAssets, currency)} across your accounts, no upcoming bills.";

        return $"{FormatAmount(liquidAssets, currency)} in accounts · {FormatAmount(totalBillsDue, currency)} committed to bills.";
    }
}
