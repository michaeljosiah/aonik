using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.Orders;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the optional <see cref="CustomerInsightOrderHistory"/> section: order
/// counts by status, the 50 most recent orders projected as
/// <see cref="CustomerInsightRecentOrder"/>s and per-order-type summaries.
/// </summary>
internal static class CustomerInsightOrderHistoryBuilder
{
    public static CustomerInsightOrderHistory Build(
        IReadOnlyList<Order> orders,
        DateTime windowStartUtc,
        DateTime windowEndUtc)
    {
        var completedCount = orders.Count(x => x.Status == OrderStatuses.Complete);
        var failedCount = orders.Count(x => x.Status is OrderStatuses.Failed or OrderStatuses.Cancelled or OrderStatuses.Expired);
        var pendingCount = orders.Count(x => x.Status is OrderStatuses.Pending or OrderStatuses.UnderReview or OrderStatuses.Approved or OrderStatuses.Transmitted or OrderStatuses.Draft);

        var recentOrders = orders
            .Take(50)
            .Select(x => new CustomerInsightRecentOrder(
                x.Id,
                string.IsNullOrWhiteSpace(x.OrderType) ? "Unknown" : x.OrderType.Trim(),
                string.IsNullOrWhiteSpace(x.Status) ? "Unknown" : x.Status.Trim(),
                CustomerInsightNormalization.NormalizeCurrency(x.CurrencyIn),
                decimal.Round(x.AmountIn, 2),
                x.CurrencyOut is null ? null : CustomerInsightNormalization.NormalizeCurrency(x.CurrencyOut),
                x.AmountOut.HasValue ? decimal.Round(x.AmountOut.Value, 2) : null,
                x.CreatedAt))
            .ToList();

        var byType = orders
            .GroupBy(x => string.IsNullOrWhiteSpace(x.OrderType) ? "Unknown" : x.OrderType.Trim())
            .OrderBy(x => x.Key)
            .Select(x => new CustomerInsightOrderTypeSummary(
                x.Key,
                x.Count(),
                x.Count(y => y.Status == OrderStatuses.Complete),
                x.Count(y => y.Status is OrderStatuses.Failed or OrderStatuses.Cancelled or OrderStatuses.Expired)))
            .ToList();

        return new CustomerInsightOrderHistory(
            windowStartUtc,
            windowEndUtc,
            orders.Count,
            completedCount,
            pendingCount,
            failedCount,
            recentOrders,
            byType);
    }
}
