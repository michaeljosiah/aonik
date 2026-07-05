using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Worker.Jobs;

internal interface ICustomerInsightAiSummaryJobSnapshotEnumerator
{
    Task<IReadOnlyList<CustomerInsightAiSummaryJobSnapshotTarget>> GetNextBatchAsync(
        CustomerInsightAiSummaryJobCheckpoint? checkpoint,
        int batchSize,
        CancellationToken cancellationToken = default);
}

internal sealed class CustomerInsightAiSummaryJobSnapshotEnumerator : ICustomerInsightAiSummaryJobSnapshotEnumerator
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ICustomerInsightAiSummaryReader _summaryReader;

    public CustomerInsightAiSummaryJobSnapshotEnumerator(
        PersonalFinanceDbContext financeDbContext,
        ICustomerInsightAiSummaryReader summaryReader)
    {
        _financeDbContext = financeDbContext;
        _summaryReader = summaryReader;
    }

    public async Task<IReadOnlyList<CustomerInsightAiSummaryJobSnapshotTarget>> GetNextBatchAsync(
        CustomerInsightAiSummaryJobCheckpoint? checkpoint,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // This scan is bounded by the number of *current* snapshots — one per active user
        // (Status == Current), i.e. O(active users), not the transaction-proportional blowup
        // the sibling user-enumerator had. It deliberately materialises the full current set
        // rather than SQL-paging it: the "already summarised" exclusion below is the runaway
        // guard, and it lives in a different module/DbContext (Aonik.Ai summaries), so it can
        // be neither a SQL anti-join nor a bounded page here. Capping the candidate scan would
        // risk returning empty while unprocessed snapshots still exist beyond the cap — which
        // would silently stall summary generation. So the candidate set is scanned in full and
        // the batch is taken after exclusion. Only the minimal (TenantId, UserId, Id) projection
        // crosses the wire.
        var snapshots = await _financeDbContext.CustomerInsightSnapshots
            .AcrossTenants()
            .AsNoTracking()
            .Where(x => x.Status == CustomerInsightSnapshotContract.StatusCurrent)
            .Select(x => new CustomerInsightAiSummaryJobSnapshotTarget(x.TenantId, x.UserId, x.Id))
            .ToListAsync(cancellationToken);

        // Exclude snapshots that already have a non-superseded AI summary (Current OR Failed).
        // This is the critical runaway guard — without it the cron re-bills OpenAI every cycle
        // for every active user. Snapshots only get re-summarised when a brand-new snapshot row
        // is minted (i.e. underlying data changed), or via an explicit force-regenerate path.
        var snapshotIds = snapshots.Select(x => x.CustomerInsightSnapshotId).ToList();
        var alreadyProcessedIds = await _summaryReader
            .GetSnapshotIdsWithExistingSummariesAsync(snapshotIds, cancellationToken);

        var alreadyProcessedSet = alreadyProcessedIds.ToHashSet();

        var orderedSnapshots = snapshots
            .Where(x => !alreadyProcessedSet.Contains(x.CustomerInsightSnapshotId))
            .OrderBy(x => x.TenantId)
            .ThenBy(x => x.UserId)
            .ThenBy(x => x.CustomerInsightSnapshotId)
            .ToList();

        if (checkpoint is not null)
        {
            orderedSnapshots = orderedSnapshots
                .Where(x => x.TenantId.CompareTo(checkpoint.Value.TenantId) > 0
                    || (x.TenantId == checkpoint.Value.TenantId && x.UserId.CompareTo(checkpoint.Value.UserId) > 0)
                    || (x.TenantId == checkpoint.Value.TenantId
                        && x.UserId == checkpoint.Value.UserId
                        && x.CustomerInsightSnapshotId.CompareTo(checkpoint.Value.CustomerInsightSnapshotId) > 0))
                .ToList();
        }

        return orderedSnapshots
            .Take(Math.Max(batchSize, 1))
            .ToList();
    }
}

internal readonly record struct CustomerInsightAiSummaryJobSnapshotTarget(
    Guid TenantId,
    Guid UserId,
    Guid CustomerInsightSnapshotId);
