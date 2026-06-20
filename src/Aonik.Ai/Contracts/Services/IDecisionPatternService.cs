namespace Aonik.Ai.Contracts.Services;

/// <summary>
/// Tenant-scoped "what worked" learning (Spec 041, Addition B). Patterns are aggregate, optionally
/// segment-scoped, reinforced as outcomes confirm them and superseded (never deleted) when outcomes
/// contradict them. Never user-scoped, never global, never cross-tenant.
/// </summary>
public interface IDecisionPatternService
{
    /// <summary>
    /// The strongest current patterns for a decision type and optional segment, ranked by confidence.
    /// Segment-specific patterns rank ahead of tenant-wide (null-segment) ones.
    /// </summary>
    Task<IReadOnlyList<DecisionPatternView>> GetTopPatternsAsync(
        string decisionType,
        string? segment = null,
        int limit = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a resolved outcome against the current pattern for (decisionType, segment): a confirming
    /// outcome reinforces it (observation++, confidence up); a contradicting one supersedes it and starts
    /// a fresh current pattern. Creates the first pattern when none exists.
    /// </summary>
    Task<DecisionPatternView> ReinforceAsync(
        ReinforceDecisionPatternRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Supersedes a current pattern by stamping SupersededAtUtc. Returns false if not found / already superseded.</summary>
    Task<bool> SupersedeAsync(Guid patternId, CancellationToken cancellationToken = default);
}

/// <param name="Contradicts">True when the outcome contradicts the current pattern (supersede + restart) rather than confirming it.</param>
public record ReinforceDecisionPatternRequest(
    string DecisionType,
    string? Segment,
    string Statement,
    string? PayloadJson = null,
    bool Contradicts = false);

public record DecisionPatternView(
    Guid Id,
    string DecisionType,
    string? Segment,
    string Statement,
    string? PayloadJson,
    int ObservationCount,
    decimal Confidence,
    DateTime? LastReinforcedAtUtc);
