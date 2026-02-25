using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Orders;

/// <summary>
/// Finance-module implementation of <see cref="IOrderExistenceChecker"/>.
/// Queries the Finance-owned Orders DbSet.
/// </summary>
internal class OrderExistenceChecker : IOrderExistenceChecker
{
    private readonly FinanceDbContext _dbContext;

    public OrderExistenceChecker(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> OrderExistsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .AnyAsync(order => order.Id == orderId, cancellationToken);
    }
}
