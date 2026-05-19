using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Finance;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// FinanceDbContext-backed implementation of <see cref="ICustomerOrderHistoryReader"/>.
/// Lives in Aonik.Finance so PersonalFinance (and other consumers) can read order
/// history without taking a project reference on Aonik.Finance.Entities.Orders.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class CustomerOrderHistoryReader : ICustomerOrderHistoryReader
{
    private readonly FinanceDbContext _dbContext;

    public CustomerOrderHistoryReader(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OrderHistoryItem>> GetForPartyAsync(
        Guid tenantId,
        Guid partyId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var orderIds = await _dbContext.OrderPartyRoles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PartyId == partyId)
            .Select(x => x.OrderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId
                && orderIds.Contains(o.Id)
                && o.CreatedAt >= fromUtc
                && o.CreatedAt <= toUtc)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderHistoryItem(
                o.Id,
                o.OrderType,
                o.Status,
                o.AmountIn,
                o.CurrencyIn,
                o.AmountOut,
                o.CurrencyOut,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderHistoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && orderIds.Contains(o.Id))
            .Select(o => new OrderHistoryItem(
                o.Id,
                o.OrderType,
                o.Status,
                o.AmountIn,
                o.CurrencyIn,
                o.AmountOut,
                o.CurrencyOut,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
