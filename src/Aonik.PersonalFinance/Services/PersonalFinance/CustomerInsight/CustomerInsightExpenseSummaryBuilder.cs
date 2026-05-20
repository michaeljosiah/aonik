using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightExpenseSummary"/> section: total outflows
/// per currency, fixed/variable and essential/discretionary splits, account-level
/// flows, period delta and rolling average spend. Also builds the list of
/// recurring-merchant candidates used by the merchant insights and signals.
/// </summary>
internal static class CustomerInsightExpenseSummaryBuilder
{
    public static CustomerInsightExpenseSummary Build(
        IReadOnlyList<NormalizedTransaction> operationalTransactions,
        IReadOnlyList<NormalizedTransaction> previousOperationalTransactions,
        IReadOnlyList<NormalizedTransaction> trendTransactions,
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<CustomerInsightRecurringMerchantCandidate> recurringMerchantCandidates,
        DateTime operationalWindowStartUtc,
        DateTime windowEndUtc)
    {
        var expenseTransactions = operationalTransactions.Where(x => x.IsExpense).ToList();
        var previousExpenseTransactions = previousOperationalTransactions.Where(x => x.IsExpense).ToList();
        var recurringMerchantKeys = recurringMerchantCandidates
            .Select(x => CustomerInsightNormalization.NormalizeKey(x.Merchant))
            .ToHashSet(StringComparer.Ordinal);

        var fixedExpenses = expenseTransactions
            .Where(x => CustomerInsightNormalization.FixedExpenseCategories.Contains(x.Category)
                || (!string.IsNullOrWhiteSpace(x.MerchantKey) && recurringMerchantKeys.Contains(x.MerchantKey)))
            .ToList();

        var essentialExpenses = expenseTransactions
            .Where(x => CustomerInsightNormalization.EssentialExpenseCategories.Contains(x.Category))
            .ToList();

        var discretionaryExpenses = expenseTransactions
            .Where(x => !CustomerInsightNormalization.EssentialExpenseCategories.Contains(x.Category))
            .ToList();

        var totalOutflows = CustomerInsightAggregations.GroupTransactionsByCurrency(expenseTransactions, true);
        var fixedSpend = CustomerInsightAggregations.GroupTransactionsByCurrency(fixedExpenses, true);
        var essentialSpend = CustomerInsightAggregations.GroupTransactionsByCurrency(essentialExpenses, true);
        var discretionarySpend = CustomerInsightAggregations.GroupTransactionsByCurrency(discretionaryExpenses, true);
        var fixedLookup = fixedSpend.ToDictionary(x => x.Currency, x => x.Amount, StringComparer.Ordinal);

        var variableSpend = totalOutflows
            .Select(x => new CustomerInsightMoneyAmount(
                x.Currency,
                decimal.Round(x.Amount - (fixedLookup.TryGetValue(x.Currency, out var fixedAmount) ? fixedAmount : 0m), 2)))
            .ToList();

        var averageSpend = CustomerInsightAggregations.BuildAverageSpendByCurrency(trendTransactions.Where(x => x.IsExpense).ToList());
        _ = accounts;

        return new CustomerInsightExpenseSummary(
            CustomerInsightSnapshotContract.OperationalWindowDays,
            operationalWindowStartUtc,
            windowEndUtc,
            totalOutflows,
            fixedSpend,
            variableSpend,
            essentialSpend,
            discretionarySpend,
            CustomerInsightAggregations.GroupTransactionsByAccount(expenseTransactions, true),
            CustomerInsightAggregations.BuildPeriodDeltas(expenseTransactions, previousExpenseTransactions, true),
            averageSpend);
    }

    public static List<CustomerInsightRecurringMerchantCandidate> BuildRecurringMerchantCandidates(
        IReadOnlyList<NormalizedTransaction> transactions)
    {
        return transactions
            .Where(x => x.IsExpense && !string.IsNullOrWhiteSpace(x.MerchantKey))
            .GroupBy(x => new { x.Currency, x.MerchantKey, x.MerchantDisplay })
            .Select(x => new
            {
                x.Key.Currency,
                x.Key.MerchantDisplay,
                Amount = Math.Abs(x.Sum(y => y.Amount)),
                ObservedMonths = x.Select(y => CustomerInsightWindows.GetMonthKey(y.OccurredAtUtc)).Distinct().Count(),
                TransactionCount = x.Count()
            })
            .Where(x => x.ObservedMonths >= 2)
            .OrderBy(x => x.Currency)
            .ThenByDescending(x => x.Amount)
            .ThenBy(x => x.MerchantDisplay)
            .Select(x => new CustomerInsightRecurringMerchantCandidate(
                x.MerchantDisplay,
                x.Currency,
                decimal.Round(x.Amount / x.ObservedMonths, 2),
                x.ObservedMonths,
                x.TransactionCount))
            .ToList();
    }
}
