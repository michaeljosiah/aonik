using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalFinanceInsightsService : IPersonalFinanceInsightsService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PersonalFinanceInsightsService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<SpendingSummaryResponse> GetSpendingSummaryAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);

        var transactions = await QueryTransactionsAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
        var currency = EnsureSingleCurrency(transactions);

        var totalIncome = transactions.Where(item => item.Amount > 0).Sum(item => item.Amount);
        var totalExpense = Math.Abs(transactions.Where(item => item.Amount < 0).Sum(item => item.Amount));

        return new SpendingSummaryResponse(
            periodStart,
            periodEnd,
            currency,
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            transactions.Count);
    }

    public async Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);

        var transactions = await QueryTransactionsAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
        _ = EnsureSingleCurrency(transactions);

        var expenseRows = transactions.Where(item => item.Amount < 0).ToList();
        var expenseTotal = Math.Abs(expenseRows.Sum(item => item.Amount));

        if (expenseRows.Count == 0 || expenseTotal == 0)
        {
            return Array.Empty<CategorySpendingItemResponse>();
        }

        return expenseRows
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Category) ? "Uncategorized" : item.Category!)
            .Select(group =>
            {
                var categoryTotal = Math.Abs(group.Sum(item => item.Amount));
                var percentage = categoryTotal / expenseTotal * 100m;

                return new CategorySpendingItemResponse(
                    group.Key,
                    categoryTotal,
                    decimal.Round(percentage, 2),
                    group.Count());
            })
            .OrderByDescending(item => item.TotalAmount)
            .ToList();
    }

    public async Task<IReadOnlyList<MerchantSpendingItemResponse>> GetMerchantBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);

        var limit = top <= 0 ? 10 : Math.Min(top, 100);
        var transactions = await QueryTransactionsAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
        _ = EnsureSingleCurrency(transactions);

        return transactions
            .Where(item => item.Amount < 0)
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Merchant) ? "Unknown Merchant" : item.Merchant!)
            .Select(group => new MerchantSpendingItemResponse(
                group.Key,
                Math.Abs(group.Sum(item => item.Amount)),
                group.Count()))
            .OrderByDescending(item => item.TotalAmount)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<AccountSpendingItemResponse>> GetAccountBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);

        var transactions = await QueryTransactionsAsync(periodStart, periodEnd, null, cancellationToken);
        _ = EnsureSingleCurrency(transactions);

        var expenseRows = transactions.Where(item => item.Amount < 0).ToList();

        if (expenseRows.Count == 0)
        {
            return Array.Empty<AccountSpendingItemResponse>();
        }

        var accountIds = expenseRows
            .Where(item => item.PersonalAccountId.HasValue)
            .Select(item => item.PersonalAccountId!.Value)
            .Distinct()
            .ToList();

        var accountMap = await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return expenseRows
            .GroupBy(item => item.PersonalAccountId)
            .Select(group =>
            {
                var accountId = group.Key;
                var accountName = "Unassigned";

                if (accountId.HasValue && accountMap.TryGetValue(accountId.Value, out var resolvedName))
                {
                    accountName = resolvedName;
                }

                return new AccountSpendingItemResponse(
                    accountId,
                    accountName,
                    Math.Abs(group.Sum(item => item.Amount)),
                    group.Count());
            })
            .OrderByDescending(item => item.TotalAmount)
            .ToList();
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task<List<PersonalTransaction>> QueryTransactionsAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var query = _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.OccurredAt >= periodStart
                && item.OccurredAt <= periodEnd);

        if (personalAccountId.HasValue)
        {
            query = query.Where(item => item.PersonalAccountId == personalAccountId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private static string EnsureSingleCurrency(IReadOnlyList<PersonalTransaction> transactions)
    {
        var currencies = transactions
            .Select(item => item.Currency)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (currencies.Count > 1)
        {
            throw new ArgumentException("Insights cannot aggregate multiple currencies. Filter by a single account or currency-scoped period.");
        }

        return currencies.FirstOrDefault() ?? "USD";
    }

    private static void ValidatePeriod(DateTime periodStart, DateTime periodEnd)
    {
        if (periodStart == default || periodEnd == default)
        {
            throw new ArgumentException("Period start and end are required.");
        }

        if (periodEnd < periodStart)
        {
            throw new ArgumentException("Period end must be greater than or equal to period start.");
        }
    }
}
