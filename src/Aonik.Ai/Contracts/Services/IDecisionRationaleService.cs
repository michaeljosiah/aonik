namespace Aonik.Ai.Contracts.Services;

/// <summary>
/// User-scoped decision rationale (Spec 041, Addition A) layered over <c>IUserMemoryService</c>: a
/// rationale is a <c>Rationale</c> memory entry keyed <c>decision.{type}.{subject}</c> whose value
/// holds the chosen option, the conditions it depended on, and what invalidates it. Recall applies
/// condition relevance so an inapplicable prior is withheld rather than surfaced.
/// </summary>
public interface IDecisionRationaleService
{
    /// <summary>Persists (or supersedes) the rationale for a decision subject through the memory service.</summary>
    Task SaveRationaleAsync(SaveRationaleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Current rationales for a decision type whose stored conditions are relevant to the current
    /// decision. Mismatches are withheld; matches rank ahead of partial matches.
    /// </summary>
    Task<IReadOnlyList<ApplicableRationale>> GetApplicableRationalesAsync(
        Guid userId,
        string decisionType,
        IReadOnlyDictionary<string, string> currentConditions,
        CancellationToken cancellationToken = default);
}

public record SaveRationaleRequest(
    Guid UserId,
    string DecisionType,
    string SubjectGrain,
    string ChosenOption,
    IReadOnlyDictionary<string, string> Conditions,
    string StaleWhen,
    string? Outcome = null,
    Guid? AiRunId = null,
    decimal Confidence = 0.8m);

public enum RationaleRelevance
{
    /// <summary>All stored conditions hold for the current decision.</summary>
    Match = 0,

    /// <summary>Some conditions hold, some differ or are unknown — surface with a caveat.</summary>
    Partial = 1,

    /// <summary>Enough conditions conflict that the rationale no longer applies — withhold it.</summary>
    Mismatch = 2,
}

public record ApplicableRationale(
    string Key,
    string DecisionType,
    string ChosenOption,
    string StaleWhen,
    IReadOnlyDictionary<string, string> Conditions,
    string? Outcome,
    RationaleRelevance Relevance,
    decimal EffectiveConfidence);
