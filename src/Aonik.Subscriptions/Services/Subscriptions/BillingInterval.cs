using Aonik.SharedKernel.Abstractions.Subscriptions;

namespace Aonik.Subscriptions.Services.Subscriptions;

/// <summary>
/// Advances a date by a plan's billing interval (Spec 087 §5).
/// </summary>
/// <remarks>
/// Shared rather than duplicated. Signup and renewal each had their own copy, and they disagreed:
/// signup honoured <c>BillingIntervals.Year</c> while renewal always added a month, so an annual
/// subscriber was charged the annual price every month after their first year. One calculation is
/// the only way the two cannot drift again.
/// </remarks>
internal static class BillingInterval
{
    public static DateTime Add(DateTime from, string billingInterval) => billingInterval switch
    {
        BillingIntervals.Year => from.AddYears(1),
        // A free plan still has periods — that is how its allowance refreshes. Treated as monthly so
        // "6 stories a month" means the same thing whether or not money changes hands.
        _ => from.AddMonths(1)
    };
}
