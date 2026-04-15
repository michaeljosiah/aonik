using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

/// <summary>
/// Implements the cross-module contract for the UserBriefProjector.
/// Reads user memory entries via <see cref="IUserMemoryService"/> (backend-agnostic)
/// and behavioural insights from the AI module's database.
/// </summary>
internal sealed class UserBriefAiDataProvider : IUserBriefAiDataProvider
{
    // User Brief projection runs before AG-UI streaming starts. If the active
    // memory backend is degraded, fail fast and continue without memory entries
    // so chat does not sit behind repeated Qdrant timeout/retry cycles.
    private static readonly TimeSpan MemoryLoadTimeout = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IUserMemoryService _memoryService;
    private readonly AiDbContext _dbContext;
    private readonly ILogger<UserBriefAiDataProvider> _logger;

    public UserBriefAiDataProvider(
        IUserMemoryService memoryService,
        AiDbContext dbContext,
        ILogger<UserBriefAiDataProvider> logger)
    {
        _memoryService = memoryService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(MemoryLoadTimeout);

        IReadOnlyList<UserMemoryEntryResponse> entries;

        try
        {
            // Delegate to IUserMemoryService which handles confidence decay and floor filtering
            // regardless of the active backend (SQL Server or Qdrant).
            entries = await _memoryService.GetCurrentEntriesAsync(
                userId,
                cancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "User Brief memory load timed out after {TimeoutMs}ms for user {UserId} in tenant {TenantId} — continuing without memory entries",
                MemoryLoadTimeout.TotalMilliseconds,
                userId,
                tenantId);
            return Array.Empty<UserBriefMemoryEntryData>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "User Brief memory load failed for user {UserId} in tenant {TenantId} — continuing without memory entries",
                userId,
                tenantId);
            return Array.Empty<UserBriefMemoryEntryData>();
        }

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
