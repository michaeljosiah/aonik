using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Orders;

/// <summary>
/// Finance-module implementation of <see cref="ICustomerFinanceStatsProvider"/>.
/// Queries Orders, OrderPartyRoles, and PaymentIntents owned by the Finance module.
/// </summary>
internal class CustomerFinanceStatsProvider : ICustomerFinanceStatsProvider
{
    private readonly FinanceDbContext _dbContext;

    public CustomerFinanceStatsProvider(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerFinanceStats> GetStatsAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.TenantId == tenantId)
            .Where(order =>
                order.PayerPartyId == partyId ||
                _dbContext.OrderPartyRoles.Any(role =>
                    role.TenantId == tenantId && role.PartyId == partyId && role.OrderId == order.Id))
            .Select(order => new
            {
                order.Id,
                order.AmountIn,
                order.CurrencyIn,
                order.Status,
                order.CreatedAt,
                order.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var totalOrders = orders.Select(order => order.Id).Distinct().Count();

        var terminalStatuses = new[]
        {
            OrderStatuses.Complete,
            OrderStatuses.Cancelled,
            OrderStatuses.Failed,
            OrderStatuses.Expired
        };

        var nonTerminalOrders = orders
            .Where(order => !terminalStatuses.Contains(order.Status))
            .ToList();

        var openOrderCount = nonTerminalOrders
            .Select(order => order.Id)
            .Distinct()
            .Count();

        var outstandingByCurrency = nonTerminalOrders
            .GroupBy(order => order.CurrencyIn)
            .Select(group => new CustomerFinanceCurrencyAmount(group.Key, group.Sum(order => order.AmountIn)))
            .ToList();

        var capturedPayments = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.TenantId == tenantId)
            .Where(intent => intent.PayerPartyId == partyId && intent.Status == PaymentStatus.Captured.ToString())
            .Select(intent => new
            {
                intent.Amount,
                intent.Currency,
                intent.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var totalPaidByCurrency = capturedPayments
            .GroupBy(payment => payment.Currency)
            .Select(group => new CustomerFinanceCurrencyAmount(group.Key, group.Sum(payment => payment.Amount)))
            .ToList();

        // Trailing windows are computed in memory off the captured-payment
        // list. The list is bounded by the customer's lifetime payment
        // count, which for any realistic customer is small enough that an
        // additional .Where on the database wouldn't pay off.
        var nowUtc = DateTime.UtcNow;
        var twelveMonthsAgo = nowUtc.AddMonths(-12);
        var thirtyDaysAgo = nowUtc.AddDays(-30);

        var trailingTwelveMonthsByCurrency = capturedPayments
            .Where(payment => payment.CreatedAt >= twelveMonthsAgo)
            .GroupBy(payment => payment.Currency)
            .Select(group => new CustomerFinanceCurrencyAmount(group.Key, group.Sum(payment => payment.Amount)))
            .ToList();

        var trailingThirtyDaysByCurrency = capturedPayments
            .Where(payment => payment.CreatedAt >= thirtyDaysAgo)
            .GroupBy(payment => payment.Currency)
            .Select(group => new CustomerFinanceCurrencyAmount(group.Key, group.Sum(payment => payment.Amount)))
            .ToList();

        var orderActivityAt = orders.Count == 0
            ? (DateTime?)null
            : orders.Max(order => order.UpdatedAt ?? order.CreatedAt);

        var paymentActivityAt = await _dbContext.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.TenantId == tenantId && intent.PayerPartyId == partyId)
            .MaxAsync(intent => (DateTime?)intent.CreatedAt, cancellationToken);

        var lastActivityAt = orderActivityAt;
        if (paymentActivityAt.HasValue && (!lastActivityAt.HasValue || paymentActivityAt > lastActivityAt))
        {
            lastActivityAt = paymentActivityAt;
        }

        return new CustomerFinanceStats(
            totalOrders,
            totalPaidByCurrency,
            outstandingByCurrency,
            lastActivityAt,
            openOrderCount,
            trailingTwelveMonthsByCurrency,
            trailingThirtyDaysByCurrency);
    }
}
