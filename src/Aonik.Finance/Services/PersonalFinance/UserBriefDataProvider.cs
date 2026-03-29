using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Implements the cross-module contract for the UserBriefProjector.
/// Queries Finance domain entities directly rather than going through the FLG
/// for a more targeted, lightweight payload.
/// </summary>
internal sealed class UserBriefDataProvider : IUserBriefDataProvider
{
    private readonly FinanceDbContext _dbContext;
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IBudgetService _budgetService;

    public UserBriefDataProvider(
        FinanceDbContext dbContext,
        IPersonalFinanceInsightsService insightsService,
        IBudgetService budgetService)
    {
        _dbContext = dbContext;
        _insightsService = insightsService;
        _budgetService = budgetService;
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

        // Parallel data loading
        var accountsTask = _dbContext.PersonalAccounts
            .Where(a => a.TenantId == tenantId && a.UserId == userId && a.Status != "archived")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var billsTask = _dbContext.Bills
            .Where(b => b.TenantId == tenantId && b.UserId == userId
                && b.Status == "active" && b.NextDueDate <= billCutoff)
            .OrderBy(b => b.NextDueDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var subscriptionsTask = _dbContext.Subscriptions
            .Where(s => s.TenantId == tenantId && s.UserId == userId && s.Status == "active")
            .OrderBy(s => s.RenewalDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var goalsTask = _dbContext.Goals
            .Where(g => g.TenantId == tenantId && g.UserId == userId && g.Status == "active")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var spendTask = _insightsService.GetSpendingSummaryAsync(spendStart, spendEnd, null, cancellationToken);
        var categoryTask = _insightsService.GetCategoryBreakdownAsync(spendStart, spendEnd, null, cancellationToken);
        var budgetTask = _budgetService.ListBudgetsAsync(cancellationToken);

        await Task.WhenAll(accountsTask, billsTask, subscriptionsTask, goalsTask, spendTask, categoryTask, budgetTask);

        var accounts = await accountsTask;
        var bills = await billsTask;
        var subscriptions = await subscriptionsTask;
        var goals = await goalsTask;
        var spend = await spendTask;
        var categories = await categoryTask;
        var budgets = await budgetTask;

        // Derive cash summary
        var primaryCurrency = accounts.FirstOrDefault()?.Currency ?? "GBP";
        var totalBalance = accounts.Sum(a => a.CurrentBalance);

        // Derive budget pressure
        var budgetPressure = DeriveBudgetPressure(budgets, categories);

        // Derive corridor countries and household context from profile
        var profile = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId, cancellationToken);

        // Derive support obligations from party relationships
        var obligations = await DeriveObligationsAsync(tenantId, userId, profile, cancellationToken);

        // Corridor countries — derived from account currencies
        var corridorCountries = DeriveCorridorCountries(accounts);

        return new UserBriefFinancialData(
            TotalBalance: totalBalance,
            AvailableBalance: totalBalance, // Simplified — real impl would subtract pending obligations
            PrimaryCurrency: primaryCurrency,
            UpcomingBills: bills.Select(b => new UserBriefBillData(
                b.Id, b.Payee, b.ExpectedAmount, b.Currency, b.NextDueDate, b.Autopay)).ToList(),
            ActiveSubscriptions: subscriptions.Select(s => new UserBriefSubscriptionData(
                s.Id, s.Merchant, s.ExpectedAmount, s.Currency, s.RenewalDate)).ToList(),
            SpendSummary: new UserBriefSpendData(
                spend.TotalExpense,
                categories.Take(5).Select(c => new UserBriefCategorySpendData(
                    c.Category, c.TotalAmount, c.Percentage)).ToList(),
                spend.PeriodStart,
                spend.PeriodEnd),
            BudgetPressure: budgetPressure,
            ActiveGoals: goals.Select(g => new UserBriefGoalData(
                g.Id, g.Name, g.TargetAmount, g.ProgressAmount, g.Currency, g.TargetDate, g.Status)).ToList(),
            SupportObligations: obligations,
            CorridorCountries: corridorCountries,
            HouseholdContext: null); // Hydrated from FLG if household exists
    }

    private static List<UserBriefBudgetPressureData> DeriveBudgetPressure(
        IReadOnlyList<Aonik.Finance.Contracts.Models.PersonalFinance.BudgetCategoryResponse> budgets,
        IReadOnlyList<Aonik.Finance.Contracts.Models.PersonalFinance.CategorySpendingItemResponse> categories)
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
