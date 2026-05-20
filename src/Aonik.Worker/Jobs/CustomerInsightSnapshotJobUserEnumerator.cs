using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Worker.Jobs;

internal interface ICustomerInsightSnapshotJobUserEnumerator
{
    Task<IReadOnlyList<CustomerInsightSnapshotJobUserTarget>> GetNextBatchAsync(
        CustomerInsightSnapshotJobCheckpoint? checkpoint,
        int batchSize,
        CancellationToken cancellationToken = default);
}

internal sealed class CustomerInsightSnapshotJobUserEnumerator : ICustomerInsightSnapshotJobUserEnumerator
{
    private readonly PersonalFinanceDbContext _financeDbContext;

    public CustomerInsightSnapshotJobUserEnumerator(PersonalFinanceDbContext financeDbContext)
    {
        _financeDbContext = financeDbContext;
    }

    public async Task<IReadOnlyList<CustomerInsightSnapshotJobUserTarget>> GetNextBatchAsync(
        CustomerInsightSnapshotJobCheckpoint? checkpoint,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var users = await _financeDbContext.PersonalProfiles
            .AcrossTenants()
            .Select(x => new { x.TenantId, x.UserId })
            .Concat(_financeDbContext.PersonalAccounts.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.PersonalTransactions.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Bills.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Subscriptions.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Goals.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Budgets.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var orderedUsers = users
            .Select(x => new CustomerInsightSnapshotJobUserTarget(x.TenantId, x.UserId))
            .Distinct()
            .OrderBy(x => x.TenantId)
            .ThenBy(x => x.UserId)
            .ToList();

        if (checkpoint is not null)
        {
            orderedUsers = orderedUsers
                .Where(x => x.TenantId.CompareTo(checkpoint.Value.TenantId) > 0
                    || (x.TenantId == checkpoint.Value.TenantId && x.UserId.CompareTo(checkpoint.Value.UserId) > 0))
                .ToList();
        }

        return orderedUsers
            .Take(Math.Max(batchSize, 1))
            .ToList();
    }
}

internal readonly record struct CustomerInsightSnapshotJobUserTarget(Guid TenantId, Guid UserId);
