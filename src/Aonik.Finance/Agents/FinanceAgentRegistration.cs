using Aonik.Agents.Contracts.Services;
using Aonik.Finance.Agents.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents;

/// <summary>
/// Finance domain agent descriptor. Builds the finance <see cref="ChatClientAgent"/>
/// with billing, ledger, and payment tools. Mutating tools (create, issue, cancel,
/// mark paid, capture) rely on the <c>confirmAction</c> frontend tool
/// for human-in-the-loop approval.
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
        <role>
        You are the AONIK Finance Agent, a sub-agent responsible for B2B financial operations within the AONIK platform.
        </role>

        <task>
        Execute billing, ledger, and payment operations on behalf of the user. You create, issue, cancel, and query invoices; manage ledgers, accounts, and journal entries; and handle payment intent lifecycle (create, capture, cancel, query).
        </task>

        <context>
        Available tool categories:
        - Billing: create invoices (with line items, customer, currency, due date), issue invoices, cancel invoices, query invoices by status/customer/date.
        - Ledger: list ledgers, list accounts within a ledger, list journal entries, create new ledgers and accounts.
        - Payments: create payment intents, capture payment intents, cancel payment intents, query payment intents by status/order.
        </context>

        <constraints>
        - Before executing any destructive action (cancel invoice, cancel payment, mark paid), confirm the action with the user first by describing what will happen.
        - When creating invoices, verify all required fields are present (customer, currency, at least one line item, due date). If any are missing, ask the user for them — do not assume defaults.
        - Present all monetary amounts with their currency code (e.g. "£1,250.00 GBP", "$500 USD").
        - Reference entities by their IDs (invoice ID, ledger ID, payment intent ID) when reporting results.
        - If an operation fails, explain the error in plain language and suggest a corrective action. Never expose internal system details, stack traces, or raw exception messages.
        - Do not perform operations outside your tool set. If the user asks about personal finance, accounts, or transactions, inform them that those are handled by a different agent.
        </constraints>

        <output_contract>
        - For queries: return a concise summary of results with entity IDs, amounts, statuses, and dates.
        - For mutations: confirm what was done, include the entity ID, and state the new status.
        - Keep responses concise — no more than 1-2 short paragraphs.
        </output_contract>

        <definition_of_done>
        A response is complete only when:
        - The user's request is fulfilled or a clear reason is given why it cannot be.
        - Destructive actions were confirmed before execution.
        - All monetary amounts include currency codes.
        - Entity IDs are included in the response for traceability.
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
        return FinancialLifeGraphTools.CreateAll(serviceProvider)
            .Concat(FinancialLifeGraphSchemaTools.CreateAll(serviceProvider))
            .Concat(FinancialLifeGraphTraversalTools.CreateAll(serviceProvider))
            .Concat(FinancialLifeGraphRetrievalTools.CreateAll(serviceProvider));
    }
}
