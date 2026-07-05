using Aonik.Finance.Agents.Tools;
using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents;

/// <summary>
/// Finance domain agent descriptor. Builds the finance <see cref="ChatClientAgent"/>
/// with billing, ledger, and payment tools. Mutating tools (create, issue, cancel,
/// mark paid, capture) are wrapped by the server-side <see cref="IToolApprovalGate"/>
/// (Spec 032, finding C3) so they cannot execute ungated — Medium/High tools surface a
/// requires-approval result instead of running. The <c>confirmAction</c> frontend tool
/// remains only a presentation affordance, no longer the enforcement boundary.
///
/// The <c>financial-life-graph-agent</c> sub-agent (FLG schema, traversal, and
/// retrieval) was relocated to Aonik.PersonalFinance with the FLG tool surface
/// (Spec 027 S1c2 / #118); this descriptor now covers core billing, ledger, and
/// payment operations only.
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
        => BuildAgent(chatClient, serviceProvider, InstructionsText, allowedToolNames: null);

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
        => BuildAgent(chatClient, serviceProvider, instructionsOverride ?? InstructionsText, allowedToolNames);

    public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider)
    {
        // Built-in tools only — tenant-contributed tools (Spec 033) are managed separately and are
        // not part of the agent's configurable toolset allow-list.
        var gate = serviceProvider.GetRequiredService<IToolApprovalGate>();
        return gate.GateAll(GetBuiltInTools(serviceProvider), serviceProvider)
            .Select(t => t.Name)
            .ToList();
    }

    // Spec 033 §8.6 — the one composition seam. Attaches the tenant's skills provider via
    // ChatClientAgentOptions.AIContextProviders and concatenates the tenant's MCP/HTTP tools into the
    // SAME gate.GateAll(...) pass as the built-ins, so tenant tools are gated exactly like built-ins
    // and there is one place a reviewer confirms gating. Agents with no tenant extensions build
    // exactly as before (empty provider list, unchanged gated tool sequence).
    private AIAgent BuildAgent(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string instructions,
        IReadOnlySet<string>? allowedToolNames)
    {
        // Fail-closed approval seam (Spec 032 C3): every mutating tool is wrapped so it cannot run
        // ungated, and an unclassified mutating-looking tool throws here at build.
        var gate = serviceProvider.GetRequiredService<IToolApprovalGate>();

        var builtIns = GetBuiltInTools(serviceProvider);

        // Tenant MCP + HTTP tools are raw here; their providers already registered each tool's
        // classification (Spec 033 §8.5), so the single GateAll pass below wraps them like built-ins.
        var tenantTools = serviceProvider.GetService<ITenantAgentToolProvider>()?.GetTools(serviceProvider)
            ?? Enumerable.Empty<AITool>();

        // Honour the IDomainAgentDescriptor contract: a non-null allow-list includes ONLY those tool
        // names — applied to tenant tools too (see ApplyToolAllowList).
        var composed = ApplyToolAllowList(builtIns.Concat(tenantTools), allowedToolNames);

        var tools = gate.GateAll(composed, serviceProvider).ToList();

        var skills = serviceProvider.GetService<ITenantSkillsProviderFactory>()?.Create(serviceProvider);

        var options = new ChatClientAgentOptions
        {
            Name = Name,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools,
            },
            AIContextProviders = skills is null
                ? new List<AIContextProvider>()
                : new List<AIContextProvider> { skills },
        };

        return new ChatClientAgent(chatClient, options);
    }

    private static IEnumerable<AITool> GetBuiltInTools(IServiceProvider serviceProvider) =>
        InvoiceTools.CreateAll(serviceProvider)
            .Concat(LedgerTools.CreateAll(serviceProvider))
            .Concat(PaymentTools.CreateAll(serviceProvider));

    /// <summary>
    /// Applies the descriptor's tool allow-list uniformly to ALL tools — built-in AND tenant-contributed
    /// (Spec 033). A non-null <paramref name="allowedToolNames"/> means "only these names"; a restricted
    /// build (voice read-only, playground with tools disabled, or a tenant toolset override) must never
    /// surface an unlisted tenant tool. Null means no restriction (every active tool is included).
    /// </summary>
    internal static IEnumerable<AITool> ApplyToolAllowList(IEnumerable<AITool> tools, IReadOnlySet<string>? allowedToolNames)
        => allowedToolNames is null ? tools : tools.Where(t => allowedToolNames.Contains(t.Name));
}
