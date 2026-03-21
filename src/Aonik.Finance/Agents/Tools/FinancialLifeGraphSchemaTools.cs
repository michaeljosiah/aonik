using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

internal sealed class FinancialLifeGraphSchemaTools
{
    private readonly IFinancialLifeGraphSchemaService _schemaService;

    private FinancialLifeGraphSchemaTools(IFinancialLifeGraphSchemaService schemaService)
    {
        _schemaService = schemaService;
    }

    [Description("Returns the complete Financial Life Graph schema — all node types with descriptions, all predicates with reasoning hints, and the full connection matrix. Use this before traversing the graph to understand what paths are available and which relationships are meaningful for the question you are answering.")]
    public GraphSchemaResponse GetGraphSchema()
    {
        return _schemaService.GetFullSchema();
    }

    [Description("Returns the schema for a specific node type — its description, all outbound predicates (edges you can follow FROM this node type), and all inbound predicates (edges that lead TO this node type). Use this when you have identified a node of interest and need to understand what traversal options are available from that node.")]
    public GraphSchemaNodeTypeResponse? GetNodeTypeSchema(
        [Description("The node type to retrieve schema for (e.g. 'PersonalAccount', 'Bill', 'Goal')")] string nodeType)
    {
        return _schemaService.GetNodeTypeSchema(nodeType);
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new FinancialLifeGraphSchemaTools(serviceProvider.GetRequiredService<IFinancialLifeGraphSchemaService>());

        yield return AIFunctionFactory.Create(tools.GetGraphSchema, name: "finance_graph_get_schema");
        yield return AIFunctionFactory.Create(tools.GetNodeTypeSchema, name: "finance_graph_get_node_type_schema");
    }
}
