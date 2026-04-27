namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Cross-module read interface that surfaces pending agent proposals for the
/// current tenant. Implemented by the Agents module and consumed by dashboards
/// and approval queues in Finance / Platform.
/// </summary>
public interface IAgentProposalQueryService
{
    /// <summary>
    /// Returns the most recent <paramref name="take"/> proposals in the
    /// <c>Proposed</c> status for the current tenant, ordered by
    /// <c>CreatedAt</c> descending. Each row is joined with the proposing
    /// agent so consumers can render its name and avatar without an extra
    /// round-trip.
    /// </summary>
    Task<IReadOnlyList<AgentProposalSummary>> ListPendingAsync(
        int take = 5,
        CancellationToken cancellationToken = default);
}
