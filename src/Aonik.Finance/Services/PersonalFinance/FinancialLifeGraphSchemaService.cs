using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphSchemaService : IFinancialLifeGraphSchemaService
{
    private readonly FinancialLifeGraphSchema _schema;

    public FinancialLifeGraphSchemaService(FinancialLifeGraphSchema schema)
    {
        _schema = schema;
    }

    public GraphSchemaResponse GetFullSchema()
    {
        var nodeTypes = _schema.NodeTypes.Values
            .OrderBy(item => item.NodeType)
            .Select(MapNodeType)
            .ToList();

        var predicates = _schema.AllEdges
            .GroupBy(item => item.Predicate, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => new GraphSchemaPredicateResponse(
                group.Key,
                group.First().ReasoningHint,
                group.Select(MapEdge).ToList()))
            .ToList();

        return new GraphSchemaResponse(nodeTypes, predicates, _schema.AllEdges.Count);
    }

    public GraphSchemaNodeTypeResponse? GetNodeTypeSchema(string nodeType)
    {
        if (!_schema.TryGetNodeType(nodeType, out var definition) || definition == null)
        {
            return null;
        }

        return MapNodeType(definition);
    }

    public string GetCompactSchemaPrompt()
    {
        return _schema.GenerateCompactSchemaPrompt();
    }

    private GraphSchemaNodeTypeResponse MapNodeType(FinancialLifeGraphNodeTypeDefinition definition)
    {
        var outbound = _schema.GetOutboundEdges(definition.NodeType).Select(MapEdge).ToList();
        var inbound = _schema.GetInboundEdges(definition.NodeType).Select(MapEdge).ToList();

        return new GraphSchemaNodeTypeResponse(
            definition.NodeType,
            definition.CanBeCreatedNatively,
            definition.IsMirrorProjection,
            definition.Description,
            outbound,
            inbound);
    }

    private static GraphSchemaEdgeRuleResponse MapEdge(FinancialLifeGraphEdgeDefinition edge)
    {
        return new GraphSchemaEdgeRuleResponse(
            edge.Predicate,
            edge.FromNodeType,
            edge.ToNodeType,
            edge.CanBeCreatedNatively,
            edge.ReasoningHint);
    }
}
