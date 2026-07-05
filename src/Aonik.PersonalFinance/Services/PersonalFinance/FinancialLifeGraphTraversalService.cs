using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;

namespace Aonik.PersonalFinance.Services;

internal sealed class FinancialLifeGraphTraversalService : IFinancialLifeGraphTraversalService
{
    private const int MaxTraversalDepth = 10;

    private readonly FinancialLifeGraphHydrationService _hydrationService;

    public FinancialLifeGraphTraversalService(FinancialLifeGraphHydrationService hydrationService)
    {
        _hydrationService = hydrationService;
    }

    public async Task<GraphNeighboursResponse> GetNeighboursAsync(
        string nodeKey,
        string? predicate = null,
        string? direction = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await GetBuiltGraphAsync(cancellationToken);
        var nodeIndex = BuildNodeIndex(graph);
        var edgeIndex = BuildEdgeIndex(graph);

        if (!nodeIndex.ContainsKey(nodeKey))
        {
            return new GraphNeighboursResponse(nodeKey, 0, [], []);
        }

        var matchingEdges = GetEdgesForNode(edgeIndex, nodeKey, predicate, direction);
        var neighbourKeys = matchingEdges
            .Select(item => item.FromNodeKey == nodeKey ? item.ToNodeKey : item.FromNodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var neighbours = neighbourKeys
            .Where(nodeIndex.ContainsKey)
            .Select(key => nodeIndex[key])
            .ToList();

        return new GraphNeighboursResponse(nodeKey, neighbours.Count, neighbours, matchingEdges);
    }

    public async Task<GraphSubgraphResponse> ExpandSubgraphAsync(
        string nodeKey,
        int maxDepth = 2,
        string? predicateFilter = null,
        CancellationToken cancellationToken = default)
    {
        maxDepth = Math.Clamp(maxDepth, 1, MaxTraversalDepth);
        var graph = await GetBuiltGraphAsync(cancellationToken);
        var nodeIndex = BuildNodeIndex(graph);
        var edgeIndex = BuildEdgeIndex(graph);

        if (!nodeIndex.ContainsKey(nodeKey))
        {
            return new GraphSubgraphResponse(nodeKey, maxDepth, 0, 0, [], []);
        }

        var visitedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nodeKey };
        var collectedEdges = new List<GraphTraversalEdgeResponse>();
        var frontier = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nodeKey };

        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var nextFrontier = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var currentKey in frontier)
            {
                var edges = GetEdgesForNode(edgeIndex, currentKey, predicateFilter, direction: null);
                foreach (var edge in edges)
                {
                    collectedEdges.Add(edge);
                    var neighbourKey = edge.FromNodeKey == currentKey ? edge.ToNodeKey : edge.FromNodeKey;
                    if (visitedNodes.Add(neighbourKey))
                    {
                        nextFrontier.Add(neighbourKey);
                    }
                }
            }

            frontier = nextFrontier;
        }

        var nodes = visitedNodes
            .Where(nodeIndex.ContainsKey)
            .Select(key => nodeIndex[key])
            .ToList();

        var dedupedEdges = DeduplicateEdges(collectedEdges);

        return new GraphSubgraphResponse(nodeKey, maxDepth, nodes.Count, dedupedEdges.Count, nodes, dedupedEdges);
    }

    public async Task<IReadOnlyList<GraphTraversalNodeResponse>> GetNodesByTypeAsync(
        string nodeType,
        CancellationToken cancellationToken = default)
    {
        var graph = await GetBuiltGraphAsync(cancellationToken);
        return graph.Nodes
            .Where(item => item.NodeType.Equals(nodeType, StringComparison.OrdinalIgnoreCase))
            .Select(MapNode)
            .ToList();
    }

    public async Task<IReadOnlyList<GraphTraversalEdgeResponse>> GetEdgesByPredicateAsync(
        string predicate,
        string? fromNodeType = null,
        string? toNodeType = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await GetBuiltGraphAsync(cancellationToken);
        var nodeIndex = BuildNodeIndex(graph);

        var edges = graph.Edges
            .Where(item => item.Predicate.Equals(predicate, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(fromNodeType))
        {
            edges = edges.Where(item =>
                nodeIndex.TryGetValue(item.FromNodeId, out var node)
                && node.NodeType.Equals(fromNodeType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(toNodeType))
        {
            edges = edges.Where(item =>
                nodeIndex.TryGetValue(item.ToNodeId, out var node)
                && node.NodeType.Equals(toNodeType, StringComparison.OrdinalIgnoreCase));
        }

        return edges.Select(MapEdge).ToList();
    }

    public async Task<GraphNodeContextResponse?> GetNodeContextAsync(
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        var graph = await GetBuiltGraphAsync(cancellationToken);
        var nodeIndex = BuildNodeIndex(graph);
        var edgeIndex = BuildEdgeIndex(graph);

        if (!nodeIndex.TryGetValue(nodeKey, out var node))
        {
            return null;
        }

        var edges = GetEdgesForNode(edgeIndex, nodeKey, predicate: null, direction: null);
        var neighbourKeys = edges
            .Select(item => item.FromNodeKey == nodeKey ? item.ToNodeKey : item.FromNodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var neighbours = neighbourKeys
            .Where(nodeIndex.ContainsKey)
            .Select(key => nodeIndex[key])
            .ToList();

        return new GraphNodeContextResponse(node, edges, neighbours);
    }

    public async Task<GraphPathResponse> FindPathAsync(
        string fromNodeKey,
        string toNodeKey,
        int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        maxDepth = Math.Clamp(maxDepth, 1, MaxTraversalDepth);
        var graph = await GetBuiltGraphAsync(cancellationToken);
        var nodeIndex = BuildNodeIndex(graph);
        var edgeIndex = BuildEdgeIndex(graph);

        if (!nodeIndex.ContainsKey(fromNodeKey) || !nodeIndex.ContainsKey(toNodeKey))
        {
            return new GraphPathResponse(fromNodeKey, toNodeKey, false, null, null, null);
        }

        if (fromNodeKey.Equals(toNodeKey, StringComparison.OrdinalIgnoreCase))
        {
            return new GraphPathResponse(fromNodeKey, toNodeKey, true, 0,
                [nodeIndex[fromNodeKey]], []);
        }

        // BFS path finding
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fromNodeKey };
        var parentMap = new Dictionary<string, (string ParentKey, GraphTraversalEdgeResponse Edge)>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(fromNodeKey);

        var found = false;

        while (queue.Count > 0 && !found)
        {
            var currentKey = queue.Dequeue();
            var currentDepth = GetPathLength(parentMap, currentKey, fromNodeKey);

            if (currentDepth >= maxDepth)
            {
                continue;
            }

            var edges = GetEdgesForNode(edgeIndex, currentKey, predicate: null, direction: null);
            foreach (var edge in edges)
            {
                var neighbourKey = edge.FromNodeKey == currentKey ? edge.ToNodeKey : edge.FromNodeKey;
                if (!visited.Add(neighbourKey))
                {
                    continue;
                }

                parentMap[neighbourKey] = (currentKey, edge);
                if (neighbourKey.Equals(toNodeKey, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }

                queue.Enqueue(neighbourKey);
            }
        }

        if (!found)
        {
            return new GraphPathResponse(fromNodeKey, toNodeKey, false, null, null, null);
        }

        // Reconstruct path
        var pathNodes = new List<GraphTraversalNodeResponse>();
        var pathEdges = new List<GraphTraversalEdgeResponse>();
        var current = toNodeKey;

        while (!current.Equals(fromNodeKey, StringComparison.OrdinalIgnoreCase))
        {
            if (nodeIndex.TryGetValue(current, out var pathNode))
            {
                pathNodes.Add(pathNode);
            }

            var (parentKey, pathEdge) = parentMap[current];
            pathEdges.Add(pathEdge);
            current = parentKey;
        }

        if (nodeIndex.TryGetValue(fromNodeKey, out var startNode))
        {
            pathNodes.Add(startNode);
        }

        pathNodes.Reverse();
        pathEdges.Reverse();

        return new GraphPathResponse(fromNodeKey, toNodeKey, true, pathEdges.Count, pathNodes, pathEdges);
    }

    private async Task<FinancialLifeGraphResponse> GetBuiltGraphAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _hydrationService.GetSnapshotAsync(cancellationToken);
        return FinancialLifeGraphService.BuildGraphFromSnapshot(snapshot);
    }

    private static Dictionary<string, GraphTraversalNodeResponse> BuildNodeIndex(FinancialLifeGraphResponse graph)
    {
        return graph.Nodes
            .GroupBy(item => item.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => MapNode(group.First()),
                StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, List<GraphTraversalEdgeResponse>> BuildEdgeIndex(FinancialLifeGraphResponse graph)
    {
        var index = new Dictionary<string, List<GraphTraversalEdgeResponse>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            var mapped = MapEdge(edge);

            if (!index.TryGetValue(edge.FromNodeId, out var fromList))
            {
                fromList = [];
                index[edge.FromNodeId] = fromList;
            }
            fromList.Add(mapped);

            if (!edge.FromNodeId.Equals(edge.ToNodeId, StringComparison.OrdinalIgnoreCase))
            {
                if (!index.TryGetValue(edge.ToNodeId, out var toList))
                {
                    toList = [];
                    index[edge.ToNodeId] = toList;
                }
                toList.Add(mapped);
            }
        }

        return index;
    }

    private static IReadOnlyList<GraphTraversalEdgeResponse> GetEdgesForNode(
        Dictionary<string, List<GraphTraversalEdgeResponse>> edgeIndex,
        string nodeKey,
        string? predicate,
        string? direction)
    {
        if (!edgeIndex.TryGetValue(nodeKey, out var edges))
        {
            return [];
        }

        IEnumerable<GraphTraversalEdgeResponse> result = edges;

        if (!string.IsNullOrWhiteSpace(predicate))
        {
            result = result.Where(item => item.Predicate.Equals(predicate, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(direction))
        {
            result = direction.ToUpperInvariant() switch
            {
                "OUTBOUND" or "OUT" => result.Where(item => item.FromNodeKey.Equals(nodeKey, StringComparison.OrdinalIgnoreCase)),
                "INBOUND" or "IN" => result.Where(item => item.ToNodeKey.Equals(nodeKey, StringComparison.OrdinalIgnoreCase)),
                _ => result
            };
        }

        return result.ToList();
    }

    private static int GetPathLength(
        Dictionary<string, (string ParentKey, GraphTraversalEdgeResponse Edge)> parentMap,
        string currentKey,
        string startKey)
    {
        var length = 0;
        var key = currentKey;
        while (!key.Equals(startKey, StringComparison.OrdinalIgnoreCase) && parentMap.ContainsKey(key))
        {
            key = parentMap[key].ParentKey;
            length++;
        }
        return length;
    }

    private static IReadOnlyList<GraphTraversalEdgeResponse> DeduplicateEdges(List<GraphTraversalEdgeResponse> edges)
    {
        return edges
            .GroupBy(item => $"{item.FromNodeKey}|{item.Predicate}|{item.ToNodeKey}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static GraphTraversalNodeResponse MapNode(FinancialLifeGraphNodeResponse node)
    {
        return new GraphTraversalNodeResponse(node.NodeId, node.NodeType, node.DisplayName, node.MetadataJson);
    }

    private static GraphTraversalEdgeResponse MapEdge(FinancialLifeGraphEdgeResponse edge)
    {
        return new GraphTraversalEdgeResponse(edge.FromNodeId, edge.Predicate, edge.ToNodeId, edge.MetadataJson);
    }
}
