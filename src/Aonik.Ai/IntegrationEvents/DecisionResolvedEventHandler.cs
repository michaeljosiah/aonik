using Aonik.Ai.Contracts.Services;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Ai.IntegrationEvents;

/// <summary>
/// Outbox handler (Spec 041, Addition C): a resolved decision feeds the outcome extractor off the
/// agent turn. The outbox dispatcher restores the originating tenant before this runs, and a failure
/// backs off through the existing outbox retry/dead-letter policy rather than affecting the source
/// transaction.
/// </summary>
internal sealed class DecisionResolvedEventHandler : IEventHandler<DecisionResolvedEvent>
{
    private readonly IDecisionOutcomeExtractor _extractor;

    public DecisionResolvedEventHandler(IDecisionOutcomeExtractor extractor) => _extractor = extractor;

    public Task HandleAsync(DecisionResolvedEvent @event, CancellationToken cancellationToken = default)
        => _extractor.ExtractAsync(@event, cancellationToken);
}
