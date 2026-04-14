using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class CustomerInsightAiSummaryReader : ICustomerInsightAiSummaryReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDbContext _dbContext;

    public CustomerInsightAiSummaryReader(AiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerInsightAiSummaryResponse?> GetCurrentSummaryForSnapshotAsync(
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var summary = await _dbContext.CustomerInsightAiSummaries
            .AsNoTracking()
            .Where(x => x.CustomerInsightSnapshotId == customerInsightSnapshotId
                && x.Status == CustomerInsightAiSummaryContract.StatusCurrent)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return summary is null ? null : Map(summary);
    }

    public async Task<CustomerInsightAiSummaryResponse?> GetSummaryAsync(
        Guid summaryId,
        CancellationToken cancellationToken = default)
    {
        var summary = await _dbContext.CustomerInsightAiSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == summaryId, cancellationToken);

        return summary is null ? null : Map(summary);
    }

    public async Task<IReadOnlyCollection<Guid>> GetSnapshotIdsWithExistingSummariesAsync(
        IReadOnlyCollection<Guid> snapshotIds,
        CancellationToken cancellationToken = default)
    {
        if (snapshotIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = await _dbContext.CustomerInsightAiSummaries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => snapshotIds.Contains(x.CustomerInsightSnapshotId)
                && (x.Status == CustomerInsightAiSummaryContract.StatusCurrent
                    || x.Status == CustomerInsightAiSummaryContract.StatusFailed))
            .Select(x => x.CustomerInsightSnapshotId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids;
    }

    private static CustomerInsightAiSummaryResponse Map(CustomerInsightAiSummary summary)
    {
        CustomerInsightAiSummaryDocument? document = null;
        if (!string.IsNullOrWhiteSpace(summary.SummaryJson))
        {
            try
            {
                document = JsonSerializer.Deserialize<CustomerInsightAiSummaryDocument>(summary.SummaryJson, JsonOptions);
            }
            catch (JsonException)
            {
                // Corrupted or incompatible stored JSON — surface the summary without a document.
            }
        }

        return new CustomerInsightAiSummaryResponse(
            summary.Id,
            summary.UserId,
            summary.CustomerInsightSnapshotId,
            summary.AiRunId,
            summary.Status,
            summary.AsOfUtc,
            summary.NarrativeVersion,
            summary.FailureReason,
            summary.SupersededById,
            summary.CreatedAt,
            summary.UpdatedAt,
            document);
    }
}
