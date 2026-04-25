using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Services;

internal interface IAiTraceReader
{
    string ProviderName { get; }

    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    Task<ListAiTraceObservationsResponse> ListObservationsAsync(
        ListAiTraceObservationsRequest request,
        CancellationToken cancellationToken = default);
}
