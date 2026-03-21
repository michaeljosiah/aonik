using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IFinancialLifeGraphTraversalService
{
    Task<GraphNeighboursResponse> GetNeighboursAsync(
        string nodeKey,
        string? predicate = null,
        string? direction = null,
        CancellationToken cancellationToken = default);

    Task<GraphSubgraphResponse> ExpandSubgraphAsync(
        string nodeKey,
        int maxDepth = 2,
        string? predicateFilter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GraphTraversalNodeResponse>> GetNodesByTypeAsync(
        string nodeType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GraphTraversalEdgeResponse>> GetEdgesByPredicateAsync(
        string predicate,
        string? fromNodeType = null,
        string? toNodeType = null,
        CancellationToken cancellationToken = default);

    Task<GraphNodeContextResponse?> GetNodeContextAsync(
        string nodeKey,
        CancellationToken cancellationToken = default);

    Task<GraphPathResponse> FindPathAsync(
        string fromNodeKey,
        string toNodeKey,
        int maxDepth = 5,
        CancellationToken cancellationToken = default);
}
