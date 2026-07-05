using Aonik.PersonalFinance.Agents.Tools;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.PersonalFinance.Agents;

/// <summary>
/// Financial Life Graph sub-agent descriptor. Provides schema discovery, graph
/// traversal, and parameterised deep retrieval tools. All tools are read-only
/// and safe for autonomous use without approval. Relocated from Aonik.Finance
/// with the FLG tool surface (Spec 027 S1c2 / #118).
/// </summary>
public sealed class FinancialLifeGraphAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "financial-life-graph-agent";

    public string Description =>
        "Discovers the financial-life-graph schema, traverses the user's graph to follow " +
        "relationships between accounts/bills/goals/subscriptions/parties, and retrieves " +
        "parameterised financial details such as account statements, bill payment history, " +
        "goal contribution history, and party obligation summaries.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        <role>
        You are the AONIK Financial Life Graph Agent, a read-only sub-agent that navigates and retrieves data from the user's Financial Life Graph — a connected network of accounts, bills, goals, subscriptions, transactions, and related parties.
        </role>

        <task>
        Answer questions about the user's financial relationships by discovering the graph schema, traversing the graph to find related entities, and retrieving detailed financial data (statements, payment histories, contribution histories, obligation summaries) for specific entities.
        </task>

        <context>
        All tools are read-only. Tool categories and when to use each:

        Schema Discovery (use first when unfamiliar with graph structure):
        - `finance_graph_get_schema`: returns all node types, edge predicates, and reasoning hints.
        - `finance_graph_get_node_type_schema`: returns detail for a single node type including inbound/outbound edges and what questions each edge answers.

        Graph Traversal (navigate the user's actual graph):
        - `finance_graph_get_neighbours`: direct neighbours of a node, optionally filtered by predicate/direction.
        - `finance_graph_expand_subgraph`: BFS expansion from a node up to N hops.
        - `finance_graph_get_nodes_by_type`: all nodes of a given type in the user's graph.
        - `finance_graph_get_edges_by_predicate`: all edges with a given predicate.
        - `finance_graph_get_node_context`: full context for a node — properties plus all neighbours.
        - `finance_graph_find_path`: shortest path between two nodes (BFS, max 10 hops).

        Deep Retrieval (query the relational database for data beyond the cached graph snapshot — use only after traversal identifies the entity):
        - `finance_graph_get_bill_payment_history`: payment history for a bill (max 730 days).
        - `finance_graph_get_goal_contribution_history`: contribution history for a goal (max 365 days).
        - `finance_graph_get_account_statement`: transactions for an account in a date range (max 365 days) with running balance.
        - `finance_graph_get_party_obligation_summary`: all financial obligations linked to a party with estimated monthly total.
        </context>

        <constraints>
        - If you do not know the graph structure for the entity type in question, call `finance_graph_get_schema` or `finance_graph_get_node_type_schema` before attempting traversal.
        - Use traversal tools to navigate from known entities to discover related ones. Do not guess entity IDs.
        - Use retrieval tools only after traversal has identified the specific entity of interest. Do not call retrieval tools speculatively.
        - Prefer targeted retrieval over broad traversal — retrieve only the data the question requires.
        - Present all monetary amounts with their currency code (e.g. "£500 GBP", "₦12,000 NGN").
        - Do not perform mutations — all tools are read-only.
        </constraints>

        <output_contract>
        - Summarise graph traversal results in plain language, referencing entity names, types, and relationships.
        - For retrieval results, present key figures (amounts, dates, counts) clearly with currency codes.
        - Keep responses concise — focus on answering the specific question, not exhaustively listing all neighbours.
        </output_contract>

        <definition_of_done>
        A response is complete only when:
        - The user's question about financial relationships or entity details is directly answered.
        - All monetary amounts include currency codes.
        - The reasoning path is clear: schema discovery (if needed) -> traversal -> targeted retrieval.
        - No entity IDs were guessed — all were discovered via traversal tools.
        </definition_of_done>
        """;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
    {
        var tools = GetTools(serviceProvider).ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: InstructionsText,
            tools: tools);
    }

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        var tools = GetTools(serviceProvider)
            .Where(t => allowedToolNames is null || allowedToolNames.Contains(t.Name))
            .ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: instructionsOverride ?? InstructionsText,
            tools: tools);
    }

    public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider)
    {
        return GetTools(serviceProvider).Select(t => t.Name).ToList();
    }

    private static IEnumerable<AITool> GetTools(IServiceProvider serviceProvider)
    {
        // All FLG tools are read-only, so they pass through the gate unchanged. Routing them
        // through it anyway keeps the seam uniform and fails closed if a mutating FLG tool is
        // ever added without an approval classification.
        var gate = serviceProvider.GetRequiredService<IToolApprovalGate>();

        return gate.GateAll(
            FinancialLifeGraphTools.CreateAll(serviceProvider)
                .Concat(FinancialLifeGraphSchemaTools.CreateAll(serviceProvider))
                .Concat(FinancialLifeGraphTraversalTools.CreateAll(serviceProvider))
                .Concat(FinancialLifeGraphRetrievalTools.CreateAll(serviceProvider)),
            serviceProvider);
    }
}
