using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Implements the cross-module contract for the UserBriefProjector.
/// Queries Finance domain entities directly rather than going through the FLG
/// for a more targeted, lightweight payload.
/// </summary>
internal sealed class UserBriefDataProvider : IUserBriefDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly FinanceDbContext _dbContext;
    private readonly IBudgetService _budgetService;
    private readonly ILogger<UserBriefDataProvider> _logger;

    public UserBriefDataProvider(
        FinanceDbContext dbContext,
        IBudgetService budgetService,
        ILogger<UserBriefDataProvider> logger)
    {
        _dbContext = dbContext;
        _budgetService = budgetService;
        _logger = logger;
    }

    public async Task<UserBriefFinancialData> GetFinancialDataAsync(
        Guid tenantId,
        Guid userId,
        UserBriefFinancialDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var billCutoff = now.AddDays(request.BillLookaheadDays);
        var spendStart = request.SpendPeriodStart ?? new DateTime(now.Year, now.Month, 1);
        var spendEnd = request.SpendPeriodEnd ?? now;

        // Sequential data loading — EF Core DbContext is not thread-safe
        var accounts = await TraceAsync(
            "aonik.user_brief.finance.accounts",
            () => _dbContext.PersonalAccounts
                .Where(a => a.TenantId == tenantId && a.UserId == userId && a.Status != "archived")
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        var bills = await TraceAsync(
            "aonik.user_brief.finance.bills",
            () => _dbContext.Bills
                .Where(b => b.TenantId == tenantId && b.UserId == userId
                    && b.Status == "active" && b.NextDueDate <= billCutoff)
                .OrderBy(b => b.NextDueDate)
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        var subscriptions = await TraceAsync(
            "aonik.user_brief.finance.subscriptions",
            () => _dbContext.Subscriptions
                .Where(s => s.TenantId == tenantId && s.UserId == userId && s.Status == "active")
                .OrderBy(s => s.RenewalDate)
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        var goals = await TraceAsync(
            "aonik.user_brief.finance.goals",
            () => _dbContext.Goals
                .Where(g => g.TenantId == tenantId && g.UserId == userId && g.Status == "active")
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        var transactionCount = await TraceAsync(
            "aonik.user_brief.finance.transaction_count",
            () => _dbContext.PersonalTransactions
                .Where(t => t.TenantId == tenantId && t.UserId == userId)
                .CountAsync(cancellationToken));

        var spendTransactions = await TraceAsync(
            "aonik.user_brief.finance.spend_transactions",
            () => _dbContext.PersonalTransactions
                .Where(t => t.TenantId == tenantId && t.UserId == userId
                    && t.OccurredAt >= spendStart && t.OccurredAt <= spendEnd)
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        var customerInsightSnapshot = await TraceAsync(
            "aonik.user_brief.finance.customer_insight_snapshot",
            () => _dbContext.CustomerInsightSnapshots
                .Where(s => s.TenantId == tenantId
                    && s.UserId == userId
                    && s.Status == CustomerInsightSnapshotContract.StatusCurrent)
                .OrderByDescending(s => s.Version)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken));

        var budgets = await TraceAsync(
            "aonik.user_brief.finance.budgets",
            () => SafeListBudgetsAsync(cancellationToken));

        // Derive cash summary
        var primaryCurrency = accounts.FirstOrDefault()?.Currency ?? "GBP";
        var totalBalance = accounts.Sum(a => a.CurrentBalance);

        // Derive spend summaries per currency so multi-currency users get full visibility
        var spendSummaries = DeriveSpendSummaries(spendTransactions, spendStart, spendEnd);

        // Derive budget pressure
        var budgetPressure = DeriveBudgetPressure(budgets);

        // Derive corridor countries and household context from profile
        var profile = await TraceAsync(
            "aonik.user_brief.finance.profile",
            () => _dbContext.PersonalProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId, cancellationToken));

        // Derive support obligations from party relationships
        var obligations = await TraceAsync(
            "aonik.user_brief.finance.obligations",
            () => DeriveObligationsAsync(tenantId, userId, profile, cancellationToken));

        // Corridor countries — derived from account currencies
        var corridorCountries = DeriveCorridorCountries(accounts);

        return new UserBriefFinancialData(
            AccountCount: accounts.Count,
            TransactionCount: transactionCount,
            TotalBalance: totalBalance,
            AvailableBalance: totalBalance, // Simplified — real impl would subtract pending obligations
            PrimaryCurrency: primaryCurrency,
            CustomerInsightSnapshot: MapCustomerInsightSnapshot(customerInsightSnapshot),
            UpcomingBills: bills.Select(b => new UserBriefBillData(
                b.Id, b.Payee, b.ExpectedAmount, b.Currency, b.NextDueDate, b.Autopay)).ToList(),
            ActiveSubscriptions: subscriptions.Select(s => new UserBriefSubscriptionData(
                s.Id, s.Merchant, s.ExpectedAmount, s.Currency, s.RenewalDate)).ToList(),
            SpendSummaries: spendSummaries,
            BudgetPressure: budgetPressure,
            ActiveGoals: goals.Select(g => new UserBriefGoalData(
                g.Id, g.Name, g.TargetAmount, g.ProgressAmount, g.Currency, g.TargetDate, g.Status)).ToList(),
            SupportObligations: obligations,
            CorridorCountries: corridorCountries,
            HouseholdContext: null); // Hydrated from FLG if household exists
    }

    private static List<UserBriefSpendData> DeriveSpendSummaries(
        List<PersonalTransaction> transactions,
        DateTime periodStart,
        DateTime periodEnd)
    {
        return transactions
            .Where(IsExpense)
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Currency) ? "USD" : t.Currency.Trim().ToUpperInvariant())
            .Select(currencyGroup =>
            {
                var expenseTotal = Math.Abs(currencyGroup.Sum(t => t.Amount));
                var topCategories = currencyGroup
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Category)
                        ? TransactionCategoryReference.Uncategorized
                        : t.Category!)
                    .Select(catGroup =>
                    {
                        var catTotal = Math.Abs(catGroup.Sum(t => t.Amount));
                        var pct = expenseTotal > 0 ? catTotal / expenseTotal * 100m : 0m;
                        return new UserBriefCategorySpendData(catGroup.Key, catTotal, pct);
                    })
                    .OrderByDescending(c => c.Amount)
                    .Take(5)
                    .ToList();

                return new UserBriefSpendData(
                    currencyGroup.Key,
                    expenseTotal,
                    topCategories,
                    periodStart,
                    periodEnd);
            })
            .OrderByDescending(s => s.TotalSpend)
            .ToList();
    }

    private static bool IsExpense(PersonalTransaction item)
    {
        if (!string.IsNullOrWhiteSpace(item.TransactionType))
            return item.TransactionType == TransactionCategoryReference.TypeExpense;
        return item.Amount < 0;
    }

    private async Task<IReadOnlyList<BudgetCategoryResponse>> SafeListBudgetsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _budgetService.ListBudgetsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Budget data unavailable for User Brief");
            return [];
        }
    }

    private static async Task<T> TraceAsync<T>(string name, Func<Task<T>> operation)
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        try
        {
            var result = await operation();
            if (result is System.Collections.ICollection collection)
            {
                activity?.SetTag("item_count", collection.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            throw;
        }
    }

    private static UserBriefCustomerInsightSnapshotData? MapCustomerInsightSnapshot(CustomerInsightSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SnapshotJson))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<CustomerInsightSnapshotDocument>(snapshot.SnapshotJson, JsonOptions);
        if (document is null)
        {
            return null;
        }

        var obligationCoverageSummaries = document.Metrics.Obligations.CoverageRatios
            .Select(x => x.Ratio.HasValue
                ? $"{x.Currency}: coverage ratio {x.Ratio.Value}"
                : $"{x.Currency}: no obligation coverage ratio available")
            .ToList();

        var budgetPressureCategories = document.Metrics.Budgets.CategoriesAboveThreshold
            .Select(x => $"{x.Category} at {x.PercentUsed}% of budget")
            .Concat(document.Metrics.Budgets.ProjectedPressureCategories.Select(x => $"{x.Category} projected to reach {x.ProjectedMonthEndAmount} {x.Currency}"))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();

        var goalHighlights = document.Metrics.Goals.ActiveGoals
            .Select(x =>
            {
                var targetTiming = x.EstimatedMonthsToTarget.HasValue
                    ? $"about {x.EstimatedMonthsToTarget.Value} months to target"
                    : "target timing unavailable";

                return $"{x.Name}: {x.ProgressPercent}% complete, {targetTiming}";
            })
            .Take(5)
            .ToList();

        var riskFlags = new List<string>();
        if (!string.Equals(document.Risk.CashflowStressLevel, CustomerInsightSnapshotContract.SeverityLow, StringComparison.OrdinalIgnoreCase))
        {
            riskFlags.Add($"Cashflow stress level: {document.Risk.CashflowStressLevel}");
        }

        if (!string.Equals(document.Risk.BudgetPressureLevel, CustomerInsightSnapshotContract.SeverityLow, StringComparison.OrdinalIgnoreCase))
        {
            riskFlags.Add($"Budget pressure level: {document.Risk.BudgetPressureLevel}");
        }

        if (!string.Equals(document.Risk.MissedObligationRisk, CustomerInsightSnapshotContract.SeverityLow, StringComparison.OrdinalIgnoreCase))
        {
            riskFlags.Add($"Missed obligation risk: {document.Risk.MissedObligationRisk}");
        }

        riskFlags.AddRange(document.Risk.ConcentrationRisks);
        riskFlags.AddRange(document.Risk.UnusualActivityIndicators);

        return new UserBriefCustomerInsightSnapshotData(
            snapshot.Id,
            document.AsOfUtc,
            document.AnalysisWindow.WindowStartUtc,
            document.AnalysisWindow.WindowEndUtc,
            document.Coverage.IsPartial,
            document.Coverage.Warnings,
            document.Metrics.CashPosition.TotalBalanceByCurrency.Select(MapMoney).ToList(),
            document.Metrics.Income.TotalInflowsByCurrency.Select(MapMoney).ToList(),
            document.Metrics.Expense.TotalOutflowsByCurrency.Select(MapMoney).ToList(),
            document.Metrics.Categories.TopCategoriesByAmount
                .Select(x => new UserBriefSnapshotSpendData(x.Category, x.Currency, x.Amount, x.ShareOfSpend))
                .Take(5)
                .ToList(),
            document.Metrics.Merchants.TopMerchantsByAmount
                .Select(x => new UserBriefSnapshotSpendData(x.Merchant, x.Currency, x.Amount, x.ShareOfSpend))
                .Take(5)
                .ToList(),
            document.Metrics.Obligations.TotalUpcomingByCurrency.Select(MapMoney).ToList(),
            obligationCoverageSummaries,
            budgetPressureCategories,
            goalHighlights,
            document.Signals
                .Select(x => new UserBriefSnapshotSignalData(
                    x.SignalKey,
                    x.Category,
                    x.Title,
                    x.Description,
                    x.Severity,
                    x.Confidence))
                .Take(5)
                .ToList(),
            riskFlags.Distinct(StringComparer.Ordinal).Take(6).ToList());
    }

    private static UserBriefSnapshotMoneyData MapMoney(CustomerInsightMoneyAmount amount) =>
        new(amount.Currency, amount.Amount);

    private static List<UserBriefBudgetPressureData> DeriveBudgetPressure(
        IReadOnlyList<BudgetCategoryResponse> budgets)
    {
        var result = new List<UserBriefBudgetPressureData>();

        foreach (var budget in budgets)
        {
            foreach (var line in budget.LineItems)
            {
                if (line.Allocated <= 0) continue;
                var percentUsed = line.Spent / line.Allocated * 100;
                if (percentUsed >= 80) // Only include categories at 80%+ of budget
                {
                    result.Add(new UserBriefBudgetPressureData(
                        line.Name, line.Allocated, line.Spent, percentUsed));
                }
            }
        }

        return result.OrderByDescending(b => b.PercentUsed).ToList();
    }

    private async Task<List<UserBriefObligationData>> DeriveObligationsAsync(
        Guid tenantId, Guid userId, PersonalProfile? profile,
        CancellationToken cancellationToken)
    {
        if (profile?.PartyId is not Guid partyId)
            return [];

        // Get upcoming bills that represent support obligations (linked to related parties)
        var upcomingObligations = await _dbContext.Bills
            .Where(b => b.TenantId == tenantId && b.UserId == userId && b.Status == "active")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // For now, return bills as obligations — real implementation would use FLG party relationships
        return upcomingObligations
            .Where(b => b.LinkedOrderId.HasValue) // Orders indicate formal service obligations
            .Select(b => new UserBriefObligationData(
                b.Payee, b.ExpectedAmount, b.Currency, b.Frequency, b.NextDueDate))
            .ToList();
    }

    private static List<string> DeriveCorridorCountries(List<PersonalAccount> accounts)
    {
        // Derive corridor from account currencies
        var currencies = accounts.Select(a => a.Currency).Distinct().ToList();
        var countries = new HashSet<string>();

        foreach (var currency in currencies)
        {
            var country = currency switch
            {
                "GBP" => "GB",
                "NGN" => "NG",
                "USD" => "US",
                "EUR" => "EU",
                "KES" => "KE",
                "GHS" => "GH",
                "ZAR" => "ZA",
                _ => null
            };
            if (country is not null) countries.Add(country);
        }

        return countries.ToList();
    }
}
