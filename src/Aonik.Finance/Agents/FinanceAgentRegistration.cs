using Aonik.Agents.Contracts.Services;
using Aonik.Finance.Agents.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents;

/// <summary>
/// Finance domain agent descriptor. Builds the finance <see cref="ChatClientAgent"/>
/// with billing, ledger, and payment tools. Mutating tools (create, issue, cancel,
/// mark paid, capture) are wrapped with <see cref="ApprovalRequiredAIFunction"/>
/// to enforce human-in-the-loop approval via the MAF proposal pattern.
///
/// The finance agent is split into two sub-agents for better LLM tool selection:
/// <list type="bullet">
///   <item><c>finance-agent</c>: Core billing, ledger, and payment operations</item>
///   <item><c>financial-life-graph-agent</c>: FLG schema, traversal, and retrieval</item>
/// </list>
/// Both are composed as tools for the master orchestrator.
/// </summary>
public sealed class FinanceAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "finance-agent";

    public string Description =>
        "Manages invoices, ledger accounts, journal entries, and payment intents for the current tenant. " +
        "Can create, issue, cancel, and query invoices; create ledgers and accounts; and manage payment intents.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        You are the AONIK Finance Agent. You help users manage their financial operations
        within the AONIK platform.

        Your capabilities include:
        - **Billing**: Create, issue, cancel, and query invoices
        - **Ledger**: List ledgers, accounts, and journal entries; create new ledgers and accounts
        - **Payments**: Create, capture, cancel, and query payment intents

        ## General Rules
        1. Always confirm destructive actions (cancel, mark paid) before executing them.
        2. When creating invoices, ensure all required fields are provided.
        3. Present monetary amounts with their currency code.
        4. Reference entities by their IDs when reporting results.
        5. If an operation fails, explain the error clearly and suggest corrective action.
        6. Never expose internal system details or raw exception messages to the user.
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
        return InvoiceTools.CreateAll(serviceProvider)
            .Concat(LedgerTools.CreateAll(serviceProvider))
            .Concat(PaymentTools.CreateAll(serviceProvider));
    }
}

/// <summary>
/// Financial Life Graph sub-agent descriptor. Provides schema discovery, graph
/// traversal, and parameterised deep retrieval tools. All tools are read-only
/// and safe for autonomous use without approval.
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
        You are the AONIK Financial Life Graph Agent. You help users explore and reason
        over their Financial Life Graph — a connected network of their accounts, bills,
        goals, subscriptions, transactions, and related parties.

        ## Schema Discovery (use first if unfamiliar with graph structure)
        - `finance_graph_get_schema`: Returns all node types, edge predicates, and reasoning hints.
        - `finance_graph_get_node_type_schema`: Returns detail for a single node type including
          its inbound/outbound edges and what questions each edge can answer.

        ## Graph Traversal (navigate the user's actual graph)
        - `finance_graph_get_neighbours`: Get direct neighbours of a node (optionally filter by predicate/direction).
        - `finance_graph_expand_subgraph`: BFS expansion from a node up to N hops.
        - `finance_graph_get_nodes_by_type`: List all nodes of a given type in the user's graph.
        - `finance_graph_get_edges_by_predicate`: List all edges with a given predicate.
        - `finance_graph_get_node_context`: Full context for a node — its properties plus all neighbours.
        - `finance_graph_find_path`: Find shortest path between two nodes (BFS, max 10 hops).

        ## Parameterised Deep Retrieval (fetch detailed financial data)
        These tools query the relational database for data that goes beyond the cached graph snapshot
        (e.g. longer time windows, aggregated computations). Use them only after traversal has
        identified the entity of interest.
        - `finance_graph_get_bill_payment_history`: Payment history for a bill (bounded window, max 730 days).
        - `finance_graph_get_goal_contribution_history`: Contribution history for a goal (bounded window, max 365 days).
        - `finance_graph_get_account_statement`: Transactions for an account in a date range (max 365 days), with running balance.
        - `finance_graph_get_party_obligation_summary`: All financial obligations linked to a party, with estimated monthly total.

        ## Reasoning Strategy
        When answering a financial question:
        1. If you don't know the graph structure, call `finance_graph_get_schema` first.
        2. Use traversal tools to navigate from known entities to discover related ones.
        3. Use retrieval tools only when you need specific financial detail (amounts, dates, history).
        4. Prefer targeted retrieval over broad traversal — retrieve only what the question requires.
        5. Always present monetary amounts with their currency code.
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
        return FinancialLifeGraphTools.CreateAll(serviceProvider)
            .Concat(FinancialLifeGraphSchemaTools.CreateAll(serviceProvider))
            .Concat(FinancialLifeGraphTraversalTools.CreateAll(serviceProvider))
            .Concat(FinancialLifeGraphRetrievalTools.CreateAll(serviceProvider));
    }
}
