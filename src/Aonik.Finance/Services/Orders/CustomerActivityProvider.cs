using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Orders;

/// <summary>
/// Finance-module implementation of <see cref="ICustomerActivityProvider"/>.
/// Returns the most recent order state changes and captured payments for a
/// party, suitable for merging with audit-log and document events into a
/// unified customer activity feed.
/// </summary>
internal class CustomerActivityProvider : ICustomerActivityProvider
{
    private readonly FinanceDbContext _dbContext;

    public CustomerActivityProvider(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomerActivityEntry>> GetRecentActivityAsync(
        Guid tenantId,
        Guid partyId,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return Array.Empty<CustomerActivityEntry>();
        }

        // Pull a slightly larger set from each source so the merged top-N
        // doesn't drop a recent payment because too many old orders were
        // fetched. Capped at 25 to keep the queries cheap.
        var perSource = Math.Min(Math.Max(take, 5), 25);

        var orderRows = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.TenantId == tenantId && order.PayerPartyId == partyId)
            .OrderByDescending(order => order.UpdatedAt ?? order.CreatedAt)
            .Take(perSource)
            .Select(order => new
            {
                order.Id,
                order.OrderType,
                order.Status,
                order.AmountIn,
                order.CurrencyIn,
                order.CreatedAt,
                order.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var paymentRows = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent =>
                intent.TenantId == tenantId &&
                intent.PayerPartyId == partyId &&
                intent.Status == PaymentStatus.Captured.ToString())
            .OrderByDescending(intent => intent.CreatedAt)
            .Take(perSource)
            .Select(intent => new
            {
                intent.Id,
                intent.Amount,
                intent.Currency,
                intent.OrderId,
                intent.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var entries = new List<CustomerActivityEntry>(orderRows.Count + paymentRows.Count);

        foreach (var order in orderRows)
        {
            // Use UpdatedAt when present so a status change reads as "just
            // now"; fall back to CreatedAt for never-updated drafts.
            var ts = order.UpdatedAt ?? order.CreatedAt;
            var amount = $"{order.CurrencyIn} {order.AmountIn:N2}";
            var kind = order.UpdatedAt.HasValue ? "order_updated" : "order_created";
            var verb = order.UpdatedAt.HasValue ? order.Status.ToLowerInvariant() : "created";
            entries.Add(new CustomerActivityEntry(
                ts,
                kind,
                $"Order {order.OrderType} · {verb}",
                amount,
                $"/orders/{order.Id}"));
        }

        foreach (var payment in paymentRows)
        {
            entries.Add(new CustomerActivityEntry(
                payment.CreatedAt,
                "payment_captured",
                "Payment captured",
                $"{payment.Currency} {payment.Amount:N2}",
                $"/orders/{payment.OrderId}"));
        }

        return entries
            .OrderByDescending(entry => entry.Timestamp)
            .Take(take)
            .ToList();
    }
}
