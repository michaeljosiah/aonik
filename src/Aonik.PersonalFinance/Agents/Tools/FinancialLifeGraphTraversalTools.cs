using System.ComponentModel;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.PersonalFinance.Agents.Tools;

internal sealed class FinancialLifeGraphTraversalTools
{
    private readonly IFinancialLifeGraphTraversalService _traversalService;

    private FinancialLifeGraphTraversalTools(IFinancialLifeGraphTraversalService traversalService)
    {
        _traversalService = traversalService;
    }

    [Description("Finds all nodes directly connected to a given node in the Financial Life Graph. Returns the node's immediate neighbours and the edges connecting them. Use this when you have identified a specific node (e.g. a PersonalAccount) and want to explore what is connected to it — transactions, bills funded by it, linked accounts, etc. You can filter by predicate (e.g. only HAS_TRANSACTION edges) and direction (OUTBOUND or INBOUND).")]
    public async Task<GraphNeighboursResponse> GetNeighbours(
        [Description("The node key to start from (format: prefix:guid, e.g. 'personal-account:abc123')")] string nodeKey,
        [Description("Optional predicate to filter edges (e.g. 'HAS_TRANSACTION', 'FUNDED_BY_ACCOUNT')")] string? predicate = null,
        [Description("Optional direction filter: 'OUTBOUND' for edges FROM this node, 'INBOUND' for edges TO this node, or omit for both")] string? direction = null,
        CancellationToken cancellationToken = default)
    {
        return await _traversalService.GetNeighboursAsync(nodeKey, predicate, direction, cancellationToken);
    }

    [Description("Expands a subgraph radiating from a starting node up to a bounded number of hops. Returns all reachable nodes and edges within the depth limit. Use this when you need to understand the neighbourhood of a node beyond just its immediate connections — e.g. starting from UserRoot with depth 2 to see accounts AND their transactions.")]
    public async Task<GraphSubgraphResponse> ExpandSubgraph(
        [Description("The node key to expand from (format: prefix:guid)")] string nodeKey,
        [Description("Maximum number of hops to traverse (1-10, default 2)")] int maxDepth = 2,
        [Description("Optional predicate to filter which edges to follow (e.g. 'OWNS_ACCOUNT')")] string? predicateFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _traversalService.ExpandSubgraphAsync(nodeKey, maxDepth, predicateFilter, cancellationToken);
    }

    [Description("Returns all nodes of a specific type in the user's Financial Life Graph. Use this when you need a complete list of a specific entity type — e.g. all bills, all goals, all subscriptions, all related parties. The output includes node keys that can be used as input to other traversal or retrieval tools.")]
    public async Task<IReadOnlyList<GraphTraversalNodeResponse>> GetNodesByType(
        [Description("The node type to retrieve (e.g. 'Bill', 'Goal', 'Subscription', 'PersonalAccount', 'Party')")] string nodeType,
        CancellationToken cancellationToken = default)
    {
        return await _traversalService.GetNodesByTypeAsync(nodeType, cancellationToken);
    }

    [Description("Returns all edges matching a specific predicate in the user's graph, optionally filtered by source or target node type. Use this to find all instances of a specific relationship — e.g. all FUNDED_BY_ACCOUNT edges to see which bills and goals are funded from which accounts.")]
    public async Task<IReadOnlyList<GraphTraversalEdgeResponse>> GetEdgesByPredicate(
        [Description("The predicate to search for (e.g. 'FUNDED_BY_ACCOUNT', 'HAS_BILL', 'RELATED_TO_PARTY')")] string predicate,
        [Description("Optional filter for the source node type")] string? fromNodeType = null,
        [Description("Optional filter for the target node type")] string? toNodeType = null,
        CancellationToken cancellationToken = default)
    {
        return await _traversalService.GetEdgesByPredicateAsync(predicate, fromNodeType, toNodeType, cancellationToken);
    }

    [Description("Returns the complete context of a single node — the node itself, all edges connected to it, and all its immediate neighbours in both directions. Use this when you have identified a node of interest and need its full relationship context before deciding whether to retrieve deeper data.")]
    public async Task<GraphNodeContextResponse?> GetNodeContext(
        [Description("The node key to retrieve context for (format: prefix:guid)")] string nodeKey,
        CancellationToken cancellationToken = default)
    {
        return await _traversalService.GetNodeContextAsync(nodeKey, cancellationToken);
    }

    [Description("Determines whether a path exists between two specific nodes in the user's graph, and if so, returns the shortest path. Use this to discover indirect relationships — e.g. whether a specific party is connected to a specific bill through intermediate nodes.")]
    public async Task<GraphPathResponse> FindPath(
        [Description("The starting node key (format: prefix:guid)")] string fromNodeKey,
        [Description("The target node key (format: prefix:guid)")] string toNodeKey,
        [Description("Maximum depth to search (1-10, default 5)")] int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        return await _traversalService.FindPathAsync(fromNodeKey, toNodeKey, maxDepth, cancellationToken);
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new FinancialLifeGraphTraversalTools(serviceProvider.GetRequiredService<IFinancialLifeGraphTraversalService>());

        yield return AIFunctionFactory.Create(tools.GetNeighbours, name: "finance_graph_get_neighbours");
        yield return AIFunctionFactory.Create(tools.ExpandSubgraph, name: "finance_graph_expand_subgraph");
        yield return AIFunctionFactory.Create(tools.GetNodesByType, name: "finance_graph_get_nodes_by_type");
        yield return AIFunctionFactory.Create(tools.GetEdgesByPredicate, name: "finance_graph_get_edges_by_predicate");
        yield return AIFunctionFactory.Create(tools.GetNodeContext, name: "finance_graph_get_node_context");
        yield return AIFunctionFactory.Create(tools.FindPath, name: "finance_graph_find_path");
    }
}
