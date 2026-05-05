using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the <see cref="CustomerInsightObligationInsights"/> section: upcoming
/// commitments aggregated from bills, subscriptions, personal recurring bills,
/// and debt repayments, plus the support-obligations subset, total upcoming
/// outflow per currency and the cash-coverage ratio against current balances.
/// </summary>
internal static class CustomerInsightObligationInsightsBuilder
{
    public static CustomerInsightObligationInsights Build(
        DateTime asOfUtc,
        IReadOnlyList<Bill> bills,
        IReadOnlyList<Subscription> subscriptions,
        IReadOnlyList<PersonalRecurringBill> personalRecurringBills,
        IReadOnlyList<DebtRepayment> debtRepayments,
        DateTime lookaheadEndUtc,
        CustomerInsightCashPosition cashPosition)
    {
        var today = asOfUtc.Date;

        var upcomingBills = bills
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => new CustomerInsightCommitmentItem(
                "Bill",
                x.Id,
                string.IsNullOrWhiteSpace(x.Payee) ? "Unnamed bill" : x.Payee.Trim(),
                CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount ?? 0m, 2),
                x.NextDueDate,
                string.IsNullOrWhiteSpace(x.Frequency) ? null : x.Frequency.Trim()))
            .ToList();

        var upcomingSubscriptions = subscriptions
            .Where(x => x.RenewalDate.Date >= today && x.RenewalDate <= lookaheadEndUtc)
            .Select(x => new CustomerInsightCommitmentItem(
                "Subscription",
                x.Id,
                string.IsNullOrWhiteSpace(x.Merchant) ? "Unnamed subscription" : x.Merchant.Trim(),
                CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount, 2),
                x.RenewalDate,
                "monthly"))
            .ToList();

        var upcomingPersonalRecurringBills = personalRecurringBills
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => new CustomerInsightCommitmentItem(
                "PersonalRecurringBill",
                x.Id,
                string.IsNullOrWhiteSpace(x.Payee) ? "Unnamed recurring bill" : x.Payee.Trim(),
                CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount ?? 0m, 2),
                x.NextDueDate,
                string.IsNullOrWhiteSpace(x.Frequency) ? null : x.Frequency.Trim()))
            .ToList();

        var upcomingDebtRepayments = debtRepayments
            .Where(x => x.NextDueDate.Date >= today && x.NextDueDate <= lookaheadEndUtc && x.ExpectedAmount.HasValue)
            .Select(x => new CustomerInsightCommitmentItem(
                "DebtRepayment",
                x.Id,
                string.IsNullOrWhiteSpace(x.CreditorName) ? "Unnamed debt" : x.CreditorName.Trim(),
                CustomerInsightNormalization.NormalizeCurrency(x.Currency),
                decimal.Round(x.ExpectedAmount ?? 0m, 2),
                x.NextDueDate,
                string.IsNullOrWhiteSpace(x.Frequency) ? null : x.Frequency.Trim()))
            .ToList();

        var supportObligations = upcomingBills
            .Where(x => bills.Any(y => y.Id == x.SourceId && y.LinkedOrderId.HasValue))
            .ToList();

        var totalUpcoming = upcomingBills
            .Concat(upcomingSubscriptions)
            .Concat(upcomingPersonalRecurringBills)
            .Concat(upcomingDebtRepayments)
            .GroupBy(x => x.Currency)
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightMoneyAmount(x.Key, decimal.Round(x.Sum(y => y.Amount), 2)))
            .ToList();

        var balancesByCurrency = cashPosition.TotalBalanceByCurrency
            .ToDictionary(x => x.Currency, x => x.Amount, StringComparer.Ordinal);

        var coverageRatios = totalUpcoming
            .Select(x =>
            {
                var availableBalance = balancesByCurrency.TryGetValue(x.Currency, out var balance) ? balance : 0m;
                decimal? ratio = x.Amount <= 0m ? null : decimal.Round(availableBalance / x.Amount, 2);
                return new CustomerInsightCoverageRatio(x.Currency, availableBalance, x.Amount, ratio);
            })
            .ToList();

        return new CustomerInsightObligationInsights(
            CustomerInsightSnapshotContract.ObligationsLookaheadDays,
            today,
            lookaheadEndUtc,
            upcomingBills,
            upcomingSubscriptions,
            upcomingPersonalRecurringBills,
            upcomingDebtRepayments,
            supportObligations,
            totalUpcoming,
            coverageRatios);
    }
}
