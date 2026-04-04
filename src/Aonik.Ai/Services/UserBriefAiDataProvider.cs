using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

/// <summary>
/// Implements the cross-module contract for the UserBriefProjector.
/// Reads user memory entries and behavioural insights from the AI module's database.
/// </summary>
internal sealed class UserBriefAiDataProvider : IUserBriefAiDataProvider
{
    private const decimal ConfidenceFloor = 0.3m;
    private const decimal DecayRatePerMonth = 0.1m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDbContext _dbContext;
    private readonly IClock _clock;

    public UserBriefAiDataProvider(AiDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        List<UserMemoryEntry> entries;
        try
        {
            entries = await _dbContext.UserMemoryEntries
                .Where(e => e.TenantId == tenantId
                    && e.UserId == userId
                    && e.SupersededById == null)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
        {
            // Table does not exist yet (migration not applied). Return empty.
            return [];
        }

        return entries
            .Select(e =>
            {
                var effective = ComputeEffectiveConfidence(e, now);
                return new { Entry = e, EffectiveConfidence = effective };
            })
            .Where(x => x.EffectiveConfidence >= ConfidenceFloor)
            .Select(x => new UserBriefMemoryEntryData(
                x.Entry.EntryType.ToString(),
                x.Entry.Key,
                x.Entry.ValueJson,
                x.EffectiveConfidence,
                x.Entry.Source.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<UserBriefInsightData>> GetBehaviouralInsightsAsync(
        Guid tenantId,
        Guid userId,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var insights = await _dbContext.Insights
            .Where(i => i.TenantId == tenantId
                && i.UserId == userId
                && i.SubjectType == "UserBehaviour"
                && (i.ExpiresAt == null || i.ExpiresAt > now))
            .OrderByDescending(i => i.CreatedUtc)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return insights.Select(i => new UserBriefInsightData(
            i.SubjectType,
            i.Title,
            i.Summary,
            1.0m, // Insights don't have a confidence field; default to 1.0
            i.MetadataJson)).ToList();
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

    private static decimal ComputeEffectiveConfidence(UserMemoryEntry entry, DateTime now)
    {
        if (entry.Source == UserMemorySource.UserStated)
            return entry.Confidence;

        var daysSinceConfirmed = (decimal)(now - entry.LastConfirmedAt).TotalDays;
        var decay = daysSinceConfirmed / 30m * DecayRatePerMonth;
        var effective = entry.Confidence - decay;

        return Math.Max(effective, 0m);
    }
}
