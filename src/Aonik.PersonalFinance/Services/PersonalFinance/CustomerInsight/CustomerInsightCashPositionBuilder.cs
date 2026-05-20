using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightCashPosition"/> section: per-currency
/// totals, available balance net of upcoming obligations, account-level balance
/// breakdown and cash concentration ratios. Also computes the upcoming-obligations
/// dictionary used by the obligation insights builder.
/// </summary>
internal static class CustomerInsightCashPositionBuilder
{
    public static CustomerInsightCashPosition Build(
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyDictionary<string, decimal> upcomingObligationsByCurrency)
    {
        var totalBalanceByCurrency = accounts
            .GroupBy(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency))
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightMoneyAmount(x.Key, decimal.Round(x.Sum(y => y.CurrentBalance), 2)))
            .ToList();

        var availableBalanceByCurrency = totalBalanceByCurrency
            .Select(x =>
            {
                var committed = upcomingObligationsByCurrency.TryGetValue(x.Currency, out var amount) ? amount : 0m;
                return new CustomerInsightMoneyAmount(x.Currency, decimal.Round(Math.Max(x.Amount - committed, 0m), 2));
            })
            .ToList();

        var absoluteTotals = accounts
            .GroupBy(x => CustomerInsightNormalization.NormalizeCurrency(x.Currency))
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => Math.Abs(y.CurrentBalance)),
                StringComparer.Ordinal);

        var balancesByAccount = accounts
            .Select(x =>
            {
                var currency = CustomerInsightNormalization.NormalizeCurrency(x.Currency);
                var absoluteTotal = absoluteTotals.TryGetValue(currency, out var total) ? total : 0m;
                var balanceShare = absoluteTotal <= 0m ? 0m : Math.Abs(x.CurrentBalance) / absoluteTotal * 100m;

                return new CustomerInsightAccountBalance(
                    x.Id,
                    string.IsNullOrWhiteSpace(x.Name) ? "Unnamed account" : x.Name.Trim(),
                    string.IsNullOrWhiteSpace(x.AccountType) ? "Unknown" : x.AccountType.Trim(),
                    currency,
                    decimal.Round(x.CurrentBalance, 2),
                    decimal.Round(balanceShare, 2));
            })
            .OrderBy(x => x.Currency)
            .ThenByDescending(x => Math.Abs(x.CurrentBalance))
            .ThenBy(x => x.AccountId)
            .ToList();

        var concentration = absoluteTotals
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var highestBalance = accounts
                    .Where(y => CustomerInsightNormalization.NormalizeCurrency(y.Currency) == x.Key)
                    .Select(y => Math.Abs(y.CurrentBalance))
                    .DefaultIfEmpty(0m)
                    .Max();

                var ratio = x.Value <= 0m ? 0m : highestBalance / x.Value * 100m;
                return new CustomerInsightConcentrationRatio(x.Key, decimal.Round(ratio, 2));
            })
            .ToList();

        return new CustomerInsightCashPosition(
            accounts.Count,
            totalBalanceByCurrency,
            availableBalanceByCurrency,
            balancesByAccount,
            concentration);
    }

    public static IReadOnlyDictionary<string, decimal> ComputeUpcomingObligationsByCurrency(
        DateTime asOfUtc,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<PersonalRecurringBill> personalRecurringBills,
        IReadOnlyList<DebtRepayment> debtRepayments,
        DateTime lookaheadEndUtc)
    {
        var today = asOfUtc.Date;
        return bills
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => (Currency: CustomerInsightNormalization.NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount!.Value))
            .Concat(subscriptions
                .Where(x => x.RenewalDate.Date >= today && x.RenewalDate <= lookaheadEndUtc)
                .Select(x => (Currency: CustomerInsightNormalization.NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount)))
            .Concat(personalRecurringBills
                .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
                .Select(x => (Currency: CustomerInsightNormalization.NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount!.Value)))
            .Concat(debtRepayments
                .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
                .Select(x => (Currency: CustomerInsightNormalization.NormalizeCurrency(x.Currency), Amount: x.ExpectedAmount!.Value)))
            .GroupBy(x => x.Currency, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount), StringComparer.Ordinal);
    }
}
