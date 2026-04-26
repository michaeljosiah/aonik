using System.Diagnostics;
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
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.user_brief.memory.current_entries", ActivityKind.Internal);
        activity?.SetTag("aonik.tenant_id", tenantId.ToString());
        activity?.SetTag("aonik.user_id", userId.ToString());
        activity?.SetTag("aonik.user_brief.memory_timeout_ms", MemoryLoadTimeout.TotalMilliseconds);

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
            activity?.SetStatus(ActivityStatusCode.Error, "memory load timeout");
            activity?.SetTag("error.type", nameof(TimeoutException));
            _logger.LogWarning(
                "User Brief memory load timed out after {TimeoutMs}ms for user {UserId} in tenant {TenantId} — continuing without memory entries",
                MemoryLoadTimeout.TotalMilliseconds,
                userId,
                tenantId);
            return Array.Empty<UserBriefMemoryEntryData>();
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogWarning(
                ex,
                "User Brief memory load failed for user {UserId} in tenant {TenantId} — continuing without memory entries",
                userId,
                tenantId);
            return Array.Empty<UserBriefMemoryEntryData>();
        }

        activity?.SetTag("aonik.user_brief.memory_entry_count", entries.Count);

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
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.user_brief.ai_summary.current", ActivityKind.Internal);
        activity?.SetTag("aonik.tenant_id", tenantId.ToString());
        activity?.SetTag("aonik.user_id", userId.ToString());
        activity?.SetTag("aonik.customer_insight_snapshot_id", customerInsightSnapshotId.ToString());

        var summary = await _dbContext.CustomerInsightAiSummaries
            .Where(x => x.TenantId == tenantId
                && x.UserId == userId
                && x.CustomerInsightSnapshotId == customerInsightSnapshotId
                && x.Status == CustomerInsightAiSummaryContract.StatusCurrent)
            .OrderByDescending(x => x.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        activity?.SetTag("aonik.user_brief.has_ai_summary", summary is not null);

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
