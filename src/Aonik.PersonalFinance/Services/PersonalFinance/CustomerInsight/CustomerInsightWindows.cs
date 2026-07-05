using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

/// <summary>
/// Resolves the analysis windows (operational, trend, behaviour, lookahead)
/// and provides month-key utilities used across snapshot generation.
/// All windows are derived deterministically from the snapshot "as of" UTC
/// and the constants on <see cref="CustomerInsightSnapshotContract"/>.
/// </summary>
internal static class CustomerInsightWindows
{
    public static DateTime ResolveWindowEnd(DateTime nowUtc) =>
        nowUtc.Date.AddDays(1).AddTicks(-1);

    public static DateTime ResolveOperationalWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.OperationalWindowDays - 1));

    public static DateTime ResolveTrendWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.TrendWindowDays - 1));

    public static DateTime ResolveBehaviourWindowStart(DateTime nowUtc) =>
        nowUtc.Date.AddDays(-(CustomerInsightSnapshotContract.BehaviourWindowDays - 1));

    public static DateTime ResolveLookaheadEnd(DateTime nowUtc) =>
        nowUtc.Date.AddDays(CustomerInsightSnapshotContract.ObligationsLookaheadDays).AddTicks(-1);

    public static DateTime ResolveBudgetPeriodEnd(DateTime periodStartUtc, string periodType)
    {
        if (string.Equals(periodType, "Weekly", StringComparison.OrdinalIgnoreCase))
        {
            return periodStartUtc.AddDays(7).AddTicks(-1);
        }

        return periodStartUtc.AddMonths(1).AddTicks(-1);
    }

    public static DateTime StartOfMonth(DateTime value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public static string GetMonthKey(DateTime value) =>
        $"{value.Year:D4}-{value.Month:D2}";
}
