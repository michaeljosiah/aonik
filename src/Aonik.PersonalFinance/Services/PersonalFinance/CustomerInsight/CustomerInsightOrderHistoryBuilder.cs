using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Finance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the optional <see cref="CustomerInsightOrderHistory"/> section: order
/// counts by status, the 50 most recent orders projected as
/// <see cref="CustomerInsightRecentOrder"/>s and per-order-type summaries.
///
/// Consumes <see cref="OrderHistoryItem"/> from
/// <see cref="ICustomerOrderHistoryReader"/> rather than the Finance Order entity
/// so this builder can move into PersonalFinance once the cluster is relocated
/// (Spec 027).
/// </summary>
internal static class CustomerInsightOrderHistoryBuilder
{
    public static CustomerInsightOrderHistory Build(
        IReadOnlyList<OrderHistoryItem> orders,
        DateTime windowStartUtc,
        DateTime windowEndUtc)
    {
        var completedCount = orders.Count(x => x.Status == OrderStatusCodes.Complete);
        var failedCount = orders.Count(x => x.Status is OrderStatusCodes.Failed or OrderStatusCodes.Cancelled or OrderStatusCodes.Expired);
        var pendingCount = orders.Count(x => x.Status is OrderStatusCodes.Pending or OrderStatusCodes.UnderReview or OrderStatusCodes.Approved or OrderStatusCodes.Transmitted or OrderStatusCodes.Draft);

        var recentOrders = orders
            .Take(50)
            .Select(x => new CustomerInsightRecentOrder(
                x.OrderId,
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
                x.Count(y => y.Status == OrderStatusCodes.Complete),
                x.Count(y => y.Status is OrderStatusCodes.Failed or OrderStatusCodes.Cancelled or OrderStatusCodes.Expired)))
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
