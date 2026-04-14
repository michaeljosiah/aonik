using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
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
    private readonly FinanceDbContext _financeDbContext;
    private readonly ICustomerInsightAiSummaryReader _summaryReader;

    public CustomerInsightAiSummaryJobSnapshotEnumerator(
        FinanceDbContext financeDbContext,
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
        var snapshots = await _financeDbContext.CustomerInsightSnapshots
            .IgnoreQueryFilters()
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
