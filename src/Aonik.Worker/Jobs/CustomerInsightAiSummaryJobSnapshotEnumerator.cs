using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
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

    public CustomerInsightAiSummaryJobSnapshotEnumerator(FinanceDbContext financeDbContext)
    {
        _financeDbContext = financeDbContext;
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

        var orderedSnapshots = snapshots
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
