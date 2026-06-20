using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Ai.Contracts.Services;

/// <summary>
/// Turns a resolved decision (Spec 041, Addition C) into learning: it reinforces the tenant
/// <c>DecisionPattern</c> for the decision type/segment and, when the user and choice subject are
/// known, writes a per-user rationale. Runs off the agent turn (driven by the outbox handler), so a
/// failure here never blocks the originating transaction.
/// </summary>
public interface IDecisionOutcomeExtractor
{
    Task ExtractAsync(DecisionResolvedEvent resolvedEvent, CancellationToken cancellationToken = default);
}
