using System.Text.Json;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;

namespace Aonik.Ai.Services;

/// <summary>
/// Rationale memory over the existing <see cref="IUserMemoryService"/> (Spec 041, Addition A). A
/// rationale is a <c>Rationale</c> entry keyed <c>decision.{type}.{subject}</c>; the structured
/// payload (chosen option, conditions, stale-when, outcome) lives in <c>ValueJson</c> — no new
/// column, no migration. Recall is where the new behaviour lives: condition relevance decides whether
/// a current rationale is surfaced as a prior, surfaced with a caveat, or withheld.
/// </summary>
internal sealed class DecisionRationaleService : IDecisionRationaleService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IUserMemoryService _memory;

    public DecisionRationaleService(IUserMemoryService memory) => _memory = memory;

    public async Task SaveRationaleAsync(SaveRationaleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DecisionType))
        {
            throw new ArgumentException("A decision type is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SubjectGrain))
        {
            throw new ArgumentException("A subject grain is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ChosenOption))
        {
            throw new ArgumentException("A chosen option is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.StaleWhen))
        {
            throw new ArgumentException("A stale-when note is required.", nameof(request));
        }

        var decisionType = request.DecisionType.Trim();
        var key = $"decision.{decisionType}.{request.SubjectGrain.Trim()}";
        var payload = new RationalePayload(
            request.ChosenOption.Trim(),
            request.Conditions ?? new Dictionary<string, string>(),
            request.StaleWhen.Trim(),
            request.Outcome);

        // Goes through the existing memory write path: same supersede-not-delete history, decay, and
        // tenant scoping as any other entry. Re-saving the same subject supersedes the prior rationale.
        await _memory.SetEntryAsync(
            new SetUserMemoryEntryRequest(
                request.UserId,
                UserMemoryEntryType.Rationale,
                key,
                JsonSerializer.Serialize(payload, Json),
                Math.Clamp(request.Confidence, 0m, 1m),
                UserMemorySource.AiInferred,
                request.AiRunId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicableRationale>> GetApplicableRationalesAsync(
        Guid userId,
        string decisionType,
        IReadOnlyDictionary<string, string> currentConditions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(decisionType))
        {
            return [];
        }

        var prefix = $"decision.{decisionType.Trim()}.";
        var current = currentConditions ?? new Dictionary<string, string>();

        // GetCurrentEntriesAsync already filters to current (non-superseded) entries and applies the
        // confidence floor (decayed/inferred entries below the floor are excluded) — RQ2 confidence rule.
        var entries = await _memory.GetCurrentEntriesAsync(userId, UserMemoryEntryType.Rationale, cancellationToken);

        var results = new List<ApplicableRationale>();
        foreach (var entry in entries)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            RationalePayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RationalePayload>(entry.ValueJson, Json);
            }
            catch (JsonException)
            {
                continue; // a malformed rationale is skipped, never thrown to the caller
            }

            if (payload is null)
            {
                continue;
            }

            var relevance = Evaluate(payload.Conditions, current);
            if (relevance == RationaleRelevance.Mismatch)
            {
                continue; // withheld unless a caller explicitly asks for historical rationale
            }

            results.Add(new ApplicableRationale(
                entry.Key,
                decisionType.Trim(),
                payload.ChosenOption,
                payload.StaleWhen,
                payload.Conditions,
                payload.Outcome,
                relevance,
                entry.EffectiveConfidence));
        }

        return results
            .OrderBy(r => r.Relevance)                       // Match (0) ahead of Partial (1)
            .ThenByDescending(r => r.EffectiveConfidence)
            .ToList();
    }

    /// <summary>
    /// Deterministic condition relevance (V1, per spec §5.2): all stored conditions hold → Match;
    /// a majority conflict → Mismatch; otherwise Partial. Conditions absent from the current decision
    /// are neither a match nor a conflict. A later version may substitute a model-judged comparison.
    /// </summary>
    private static RationaleRelevance Evaluate(
        IReadOnlyDictionary<string, string> stored,
        IReadOnlyDictionary<string, string> current)
    {
        if (stored.Count == 0)
        {
            return RationaleRelevance.Match;
        }

        var matched = 0;
        var conflicting = 0;
        foreach (var (conditionKey, storedValue) in stored)
        {
            if (current.TryGetValue(conditionKey, out var currentValue))
            {
                if (string.Equals(storedValue, currentValue, StringComparison.OrdinalIgnoreCase))
                {
                    matched++;
                }
                else
                {
                    conflicting++;
                }
            }
        }

        if (conflicting == 0 && matched == stored.Count)
        {
            return RationaleRelevance.Match;
        }

        // A STRICT majority of conditions must conflict before the rationale is withheld; an even
        // split (e.g. 1 of 2) stays a caveated Partial rather than a hidden Mismatch.
        if (conflicting * 2 > stored.Count)
        {
            return RationaleRelevance.Mismatch;
        }

        return RationaleRelevance.Partial;
    }

    private sealed record RationalePayload(
        string ChosenOption,
        IReadOnlyDictionary<string, string> Conditions,
        string StaleWhen,
        string? Outcome);
}
