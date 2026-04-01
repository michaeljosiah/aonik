namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Cross-module contract for the UserBriefProjector to retrieve AI module data
/// (user memory entries and behavioural insights) without depending on the AI module directly.
/// Implemented by AiModule, consumed by AgentsModule.
/// </summary>
public interface IUserBriefAiDataProvider
{
    /// <summary>
    /// Retrieves current (non-superseded) user memory entries with confidence decay applied.
    /// </summary>
    Task<IReadOnlyList<UserBriefMemoryEntryData>> GetCurrentMemoryEntriesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves top behavioural insights for a user, filtered by confidence and expiry.
    /// </summary>
    Task<IReadOnlyList<UserBriefInsightData>> GetBehaviouralInsightsAsync(
        Guid tenantId,
        Guid userId,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current AI interpretation for the user's current deterministic
    /// customer insight snapshot, if one exists.
    /// </summary>
    Task<UserBriefCustomerInsightAiSummaryData?> GetCurrentCustomerInsightAiSummaryAsync(
        Guid tenantId,
        Guid userId,
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default);
}

public record UserBriefMemoryEntryData(
    string EntryType,
    string Key,
    string ValueJson,
    decimal EffectiveConfidence,
    string Source);

public record UserBriefInsightData(
    string InsightType,
    string Title,
    string Summary,
    decimal Confidence,
    string? MetadataJson);

public record UserBriefCustomerInsightAiSummaryData(
    string Headline,
    string Summary,
    IReadOnlyList<string> KeyObservations,
    IReadOnlyList<string> RecommendedFocusAreas,
    IReadOnlyList<string> ReferencedMetricKeys,
    IReadOnlyList<string> Caveats);
