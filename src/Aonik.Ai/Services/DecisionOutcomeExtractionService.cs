using System.Text.Json;
using Aonik.Ai.Contracts.Services;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Ai.Services;

/// <summary>
/// Decision-outcome extraction (Spec 041, Addition C). Given a resolved decision it reinforces the
/// tenant pattern for the decision type/segment (superseding it on a contradicting outcome) and, when
/// the user and choice subject are known, writes a per-user rationale. V1 is deterministic: the
/// distilled statement/conditions ride the event's <c>ContextJson</c>, written by the emitter. The
/// seam to swap in a model-judged distiller (the <c>decision-outcome-extraction</c> AI task profile,
/// recording an AiRun) is a single replacement of this service — callers and wiring are unchanged.
/// </summary>
internal sealed class DecisionOutcomeExtractionService : IDecisionOutcomeExtractor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // A contradicting terminal outcome supersedes the current pattern rather than reinforcing it.
    private static readonly HashSet<string> NegativeOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Failed", "Reversed", "Cancelled", "Canceled", "Rejected", "Abandoned",
    };

    private const int MaxStatementLength = 1024;

    private readonly IDecisionPatternService _patterns;
    private readonly IDecisionRationaleService _rationales;

    public DecisionOutcomeExtractionService(
        IDecisionPatternService patterns,
        IDecisionRationaleService rationales)
    {
        _patterns = patterns;
        _rationales = rationales;
    }

    public async Task ExtractAsync(DecisionResolvedEvent resolvedEvent, CancellationToken cancellationToken = default)
    {
        if (resolvedEvent is null || string.IsNullOrWhiteSpace(resolvedEvent.DecisionType))
        {
            return;
        }

        var context = ParseContext(resolvedEvent.ContextJson);
        var contradicts = NegativeOutcomes.Contains(resolvedEvent.Outcome ?? string.Empty);

        // Tenant pattern: reinforce "what worked", or supersede-and-restart on a contradicting outcome.
        var statement = context.Statement
            ?? $"{resolvedEvent.DecisionType} resolved as {resolvedEvent.Outcome} via {context.ChosenOption ?? "the chosen approach"}.";

        await _patterns.ReinforceAsync(
            new ReinforceDecisionPatternRequest(
                resolvedEvent.DecisionType,
                context.Segment ?? resolvedEvent.Segment,
                Truncate(statement),
                resolvedEvent.ContextJson,
                contradicts),
            cancellationToken);

        // User rationale: only when the user and the subject of the choice are both known.
        if (resolvedEvent.UserId is { } userId
            && !string.IsNullOrWhiteSpace(context.SubjectGrain)
            && !string.IsNullOrWhiteSpace(context.ChosenOption))
        {
            await _rationales.SaveRationaleAsync(
                new SaveRationaleRequest(
                    userId,
                    resolvedEvent.DecisionType,
                    context.SubjectGrain!,
                    context.ChosenOption!,
                    context.Conditions ?? new Dictionary<string, string>(),
                    context.StaleWhen ?? $"the {resolvedEvent.DecisionType} inputs change",
                    resolvedEvent.Outcome,
                    resolvedEvent.AiRunId),
                cancellationToken);
        }
    }

    private static OutcomeContext ParseContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OutcomeContext();
        }

        try
        {
            return JsonSerializer.Deserialize<OutcomeContext>(json, Json) ?? new OutcomeContext();
        }
        catch (JsonException)
        {
            return new OutcomeContext();
        }
    }

    private static string Truncate(string value)
        => value.Length <= MaxStatementLength ? value : value[..MaxStatementLength];

    private sealed record OutcomeContext
    {
        public string? Statement { get; init; }
        public string? Segment { get; init; }
        public string? SubjectGrain { get; init; }
        public string? ChosenOption { get; init; }
        public string? StaleWhen { get; init; }
        public IReadOnlyDictionary<string, string>? Conditions { get; init; }
    }
}
