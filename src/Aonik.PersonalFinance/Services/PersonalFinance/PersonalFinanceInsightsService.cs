using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalFinanceInsightsService : IPersonalFinanceInsightsService
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    // H1: classify income/expense inside SQL instead of materializing the whole period and
    // filtering in memory. Mirrors the old IsIncome/IsExpense helpers — prefer the
    // TransactionType column when populated, fall back to amount sign for legacy rows that
    // predate it. Expressed as a boolean OR-of-ANDs (not a ternary) so EF Core emits a plain
    // WHERE with no CASE. string.IsNullOrEmpty is an EF-translatable call; in SQL Server the
    // trailing-space comparison semantics also fold whitespace-only values into the empty case,
    // matching the original IsNullOrWhiteSpace intent.
    private static readonly Expression<Func<PersonalTransaction, bool>> ExpensePredicate =
        item => item.TransactionType == TransactionCategoryReference.TypeExpense
            || (string.IsNullOrEmpty(item.TransactionType) && item.Amount < 0);

    private static readonly Expression<Func<PersonalTransaction, bool>> IncomePredicate =
        item => item.TransactionType == TransactionCategoryReference.TypeIncome
            || (string.IsNullOrEmpty(item.TransactionType) && item.Amount > 0);

    public PersonalFinanceInsightsService(
        PersonalFinanceDbContext financeDbContext,
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

        var baseQuery = BuildTransactionQuery(periodStart, periodEnd, personalAccountId);

        // One small grouped pass over the period covers both currency validation and the total
        // transaction count (which includes income, transfers and uncategorised rows).
        var currencyGroups = await LoadCurrencyGroupsAsync(baseQuery, cancellationToken);
        var currency = EnsureSingleCurrency(currencyGroups.Select(group => group.RawCurrency));
        var totalCount = currencyGroups.Sum(group => group.Count);

        var totalIncome = await baseQuery
            .Where(IncomePredicate)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var totalExpenseSigned = await baseQuery
            .Where(ExpensePredicate)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var totalExpense = Math.Abs(totalExpenseSigned);

        return new SpendingSummaryResponse(
            periodStart,
            periodEnd,
            currency,
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            totalCount);
    }

    public async Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdownAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);

        var expenseQuery = BuildTransactionQuery(periodStart, periodEnd, personalAccountId)
            .Where(ExpensePredicate);

        var scope = ResolveDominantExpenseCurrency(
            await LoadCurrencyGroupsAsync(expenseQuery, cancellationToken),
            personalAccountId);

        if (scope is null)
        {
            return Array.Empty<CategorySpendingItemResponse>();
        }

        var (rawCurrencies, currency) = scope.Value;

        var categoryGroups = await expenseQuery
            .Where(item => rawCurrencies.Contains(item.Currency))
            .GroupBy(item => item.Category)
            .Select(group => new
            {
                Category = group.Key,
                Total = group.Sum(item => item.Amount),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        // Fold the null and empty-string category groups together into "Uncategorized".
        var merged = categoryGroups
            .GroupBy(row => string.IsNullOrWhiteSpace(row.Category)
                ? TransactionCategoryReference.Uncategorized
                : row.Category!)
            .Select(group => new
            {
                Category = group.Key,
                Signed = group.Sum(row => row.Total),
                Count = group.Sum(row => row.Count),
            })
            .ToList();

        var expenseTotal = Math.Abs(merged.Sum(row => row.Signed));

        if (expenseTotal == 0)
        {
            return Array.Empty<CategorySpendingItemResponse>();
        }

        return merged
            .Select(row =>
            {
                var amount = Math.Abs(row.Signed);
                return new CategorySpendingItemResponse(
                    row.Category,
                    currency,
                    amount,
                    decimal.Round(amount / expenseTotal * 100m, 2),
                    row.Count);
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

        var expenseQuery = BuildTransactionQuery(periodStart, periodEnd, personalAccountId)
            .Where(ExpensePredicate);

        var scope = ResolveDominantExpenseCurrency(
            await LoadCurrencyGroupsAsync(expenseQuery, cancellationToken),
            personalAccountId);

        if (scope is null)
        {
            return Array.Empty<MerchantSpendingItemResponse>();
        }

        var (rawCurrencies, currency) = scope.Value;

        var merchantGroups = await expenseQuery
            .Where(item => rawCurrencies.Contains(item.Currency))
            .GroupBy(item => item.Merchant)
            .Select(group => new
            {
                Merchant = group.Key,
                Total = group.Sum(item => item.Amount),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        // Fold the null and empty-string merchant groups together into "Unknown Merchant".
        return merchantGroups
            .GroupBy(row => string.IsNullOrWhiteSpace(row.Merchant) ? "Unknown Merchant" : row.Merchant!)
            .Select(group => new MerchantSpendingItemResponse(
                group.Key,
                currency,
                Math.Abs(group.Sum(row => row.Total)),
                group.Sum(row => row.Count)))
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

        var baseQuery = BuildTransactionQuery(periodStart, periodEnd, null);

        // Account breakdown enforces a single currency across ALL transactions in the period
        // (income, transfers included) rather than picking a dominant expense currency.
        var currencyGroups = await LoadCurrencyGroupsAsync(baseQuery, cancellationToken);
        _ = EnsureSingleCurrency(currencyGroups.Select(group => group.RawCurrency));

        var accountGroups = await baseQuery
            .Where(ExpensePredicate)
            .GroupBy(item => item.PersonalAccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Total = group.Sum(item => item.Amount),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        if (accountGroups.Count == 0)
        {
            return Array.Empty<AccountSpendingItemResponse>();
        }

        var accountIds = accountGroups
            .Where(group => group.AccountId.HasValue)
            .Select(group => group.AccountId!.Value)
            .Distinct()
            .ToList();

        var accountMap = await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return accountGroups
            .Select(group =>
            {
                var accountName = "Unassigned";

                if (group.AccountId.HasValue && accountMap.TryGetValue(group.AccountId.Value, out var resolvedName))
                {
                    accountName = resolvedName;
                }

                return new AccountSpendingItemResponse(
                    group.AccountId,
                    accountName,
                    Math.Abs(group.Total),
                    group.Count);
            })
            .OrderByDescending(item => item.TotalAmount)
            .ToList();
    }

    public async Task<MerchantHistoryResponse> GetMerchantHistoryAsync(
        string merchantName,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var merchantQuery = _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.Merchant == merchantName);

        var count = await merchantQuery
            .Where(ExpensePredicate)
            .CountAsync(cancellationToken);

        var totalSpentSigned = await merchantQuery
            .Where(ExpensePredicate)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var totalSpent = Math.Abs(totalSpentSigned);
        var averageSpend = count > 0 ? totalSpent / count : 0m;

        // First non-empty currency recorded against the merchant (any transaction type).
        var currencyRaw = await merchantQuery
            .Where(item => item.Currency != "")
            .Select(item => item.Currency)
            .FirstOrDefaultAsync(cancellationToken);

        var currency = NormalizeCurrency(currencyRaw);
        var symbol = GetCurrencySymbol(currency);

        return new MerchantHistoryResponse(
            TransactionCountLabel: count.ToString(),
            AverageSpendLabel: $"{symbol}{averageSpend:N2}",
            TotalSpentLabel: $"{symbol}{totalSpent:N2}");
    }

    private static string GetCurrencySymbol(string currencyCode) =>
        currencyCode switch
        {
            "GBP" => "£",
            "EUR" => "€",
            "NGN" => "₦",
            _ => "$",
        };

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    /// <summary>
    /// Builds the composable per-user, per-period transaction query. Returns an
    /// <see cref="IQueryable{T}"/> (not a materialized list) so callers can push GROUP BY/SUM/
    /// COUNT aggregation into SQL instead of pulling every row into memory (finding H1).
    /// </summary>
    private IQueryable<PersonalTransaction> BuildTransactionQuery(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? personalAccountId)
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

        return query;
    }

    /// <summary>
    /// Runs a single GROUP BY currency aggregate in SQL, returning one row per distinct raw
    /// currency string with its signed total and count. The result set is tiny (one row per
    /// currency), so the downstream currency normalization/dominance logic runs in memory.
    /// </summary>
    private static async Task<IReadOnlyList<CurrencyGroup>> LoadCurrencyGroupsAsync(
        IQueryable<PersonalTransaction> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(item => item.Currency)
            .Select(group => new
            {
                Currency = group.Key,
                Total = group.Sum(item => item.Amount),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new CurrencyGroup(row.Currency, row.Total, row.Count))
            .ToList();
    }

    /// <summary>
    /// Decides which raw currency strings feed an expense breakdown and the normalized currency
    /// label to report, from the SQL currency-group aggregate. Returns <c>null</c> when there
    /// are no expenses. Mirrors the old <c>FilterToDominantExpenseCurrency</c> +
    /// <c>EnsureSingleCurrency</c> pairing: when an account is specified there is no dominant
    /// selection (all rows are kept) and a multi-currency account throws; otherwise the
    /// dominant currency is chosen by absolute total, then count, then ordinal code.
    /// </summary>
    private static (string[] RawCurrencies, string Currency)? ResolveDominantExpenseCurrency(
        IReadOnlyList<CurrencyGroup> expenseGroups,
        Guid? personalAccountId)
    {
        if (expenseGroups.Count == 0)
        {
            return null;
        }

        var byNormalized = expenseGroups
            .GroupBy(group => NormalizeCurrency(group.RawCurrency), StringComparer.Ordinal)
            .Select(group => new
            {
                Normalized = group.Key,
                Total = group.Sum(entry => entry.Total),
                Count = group.Sum(entry => entry.Count),
            })
            .ToList();

        if (personalAccountId.HasValue && byNormalized.Count > 1)
        {
            throw new ArgumentException(
                "Insights cannot aggregate multiple currencies. Filter by a single account or currency-scoped period.");
        }

        if (personalAccountId.HasValue || byNormalized.Count <= 1)
        {
            // No dominant filtering: keep every raw currency present in the period.
            var allRaw = expenseGroups
                .Select(group => group.RawCurrency)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return (allRaw, byNormalized[0].Normalized);
        }

        var dominant = byNormalized
            .OrderByDescending(group => Math.Abs(group.Total))
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.Normalized, StringComparer.Ordinal)
            .First();

        var dominantRaw = expenseGroups
            .Where(group => NormalizeCurrency(group.RawCurrency) == dominant.Normalized)
            .Select(group => group.RawCurrency)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return (dominantRaw, dominant.Normalized);
    }

    private static string EnsureSingleCurrency(IEnumerable<string> rawCurrencies)
    {
        var currencies = rawCurrencies
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeCurrency)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (currencies.Count > 1)
        {
            throw new ArgumentException("Insights cannot aggregate multiple currencies. Filter by a single account or currency-scoped period.");
        }

        return currencies.FirstOrDefault() ?? "USD";
    }

    private static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

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

    /// <summary>One row of the SQL GROUP BY currency aggregate: raw currency, signed total, count.</summary>
    private readonly record struct CurrencyGroup(string RawCurrency, decimal Total, int Count);
}
