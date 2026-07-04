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
        // Dedup in the DATABASE (Distinct inside the query), not in memory. Previously the
        // whole 7-table union was materialised first and de-duplicated afterwards — so the
        // rows crossing the wire were proportional to total transactions/bills/subscriptions
        // across every tenant (a user with 5,000 transactions contributed 5,000 rows before
        // the in-memory Distinct collapsed them to one). Concat -> UNION ALL and Distinct ->
        // SELECT DISTINCT execute server-side, so only distinct (tenant, user) pairs return.
        // This removes the transaction-proportional term — the actual memory-spike risk. The
        // residual in-memory set is O(distinct users) (a few Guid pairs each), which is small
        // at realistic scale; bounding it further to O(batchSize) would need an offset-based
        // SQL page, but the (TenantId, UserId) keyset checkpoint below can't be expressed in
        // SQL (Guid has no translatable > operator), so that is deliberately not attempted
        // here — it would trade a working checkpoint contract for negligible practical gain.
        var users = await _financeDbContext.PersonalProfiles
            .AcrossTenants()
            .Select(x => new { x.TenantId, x.UserId })
            .Concat(_financeDbContext.PersonalAccounts.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.PersonalTransactions.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Bills.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Subscriptions.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Goals.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Concat(_financeDbContext.Budgets.AcrossTenants().Select(x => new { x.TenantId, x.UserId }))
            .Distinct()
            .ToListAsync(cancellationToken);

        var orderedUsers = users
            .Select(x => new CustomerInsightSnapshotJobUserTarget(x.TenantId, x.UserId))
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
