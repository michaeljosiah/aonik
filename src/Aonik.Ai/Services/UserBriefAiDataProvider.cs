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

        var entries = await _dbContext.UserMemoryEntries
            .Where(e => e.TenantId == tenantId
                && e.UserId == userId
                && e.SupersededById == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

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
