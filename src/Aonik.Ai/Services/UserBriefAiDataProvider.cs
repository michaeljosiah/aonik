using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

/// <summary>
/// Implements the cross-module contract for the UserBriefProjector.
/// Reads user memory entries via <see cref="IUserMemoryService"/> (backend-agnostic)
/// and behavioural insights from the AI module's database.
/// </summary>
internal sealed class UserBriefAiDataProvider : IUserBriefAiDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IUserMemoryService _memoryService;
    private readonly AiDbContext _dbContext;

    public UserBriefAiDataProvider(IUserMemoryService memoryService, AiDbContext dbContext)
    {
        _memoryService = memoryService;
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Delegate to IUserMemoryService which handles confidence decay and floor filtering
        // regardless of the active backend (SQL Server or Qdrant).
        var entries = await _memoryService.GetCurrentEntriesAsync(userId, cancellationToken: cancellationToken);

        return entries
            .Select(e => new UserBriefMemoryEntryData(
                e.EntryType.ToString(),
                e.Key,
                e.ValueJson,
                e.EffectiveConfidence,
                e.Source.ToString()))
            .ToList();
    }

    public async Task<UserBriefCustomerInsightAiSummaryData?> GetCurrentCustomerInsightAiSummaryAsync(
        Guid tenantId,
        Guid userId,
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var summary = await _dbContext.CustomerInsightAiSummaries
            .Where(x => x.TenantId == tenantId
                && x.UserId == userId
                && x.CustomerInsightSnapshotId == customerInsightSnapshotId
                && x.Status == CustomerInsightAiSummaryContract.StatusCurrent)
            .OrderByDescending(x => x.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null || string.IsNullOrWhiteSpace(summary.SummaryJson))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<CustomerInsightAiSummaryDocument>(summary.SummaryJson, JsonOptions);
        if (document is null)
        {
            return null;
        }

        return new UserBriefCustomerInsightAiSummaryData(
            document.Headline,
            document.Summary,
            document.KeyObservations,
            document.RecommendedFocusAreas,
            document.ReferencedMetrics,
            document.Caveats);
    }

}
