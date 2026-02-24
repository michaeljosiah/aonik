using Aonik.Agents.Framework;
using Aonik.Finance.Agents.Tools;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents;

/// <summary>
/// Finance domain agent. Exposes billing, ledger, and payment tools to the LLM.
/// Extends <see cref="AonikDomainAgent"/> and is composed into the master orchestrator
/// via <c>agent.AsAIFunction()</c>.
/// </summary>
public sealed class FinanceDomainAgent : AonikDomainAgent
{
    public override string Name => "finance-agent";

    public override string Description =>
        "Manages invoices, ledger accounts, journal entries, and payment intents for the current tenant.";

    protected override string Instructions =>
        """
        You are the AONIK Finance Agent. You help users manage their financial operations
        within the AONIK platform.

        Your capabilities include:
        - **Billing**: Create, issue, cancel, and query invoices
        - **Ledger**: List ledgers, accounts, and journal entries; create new ledgers and accounts
        - **Payments**: Create, capture, cancel, and query payment intents

        Rules:
        1. Always confirm destructive actions (cancel, mark paid) before executing them.
        2. When creating invoices, ensure all required fields are provided.
        3. Present monetary amounts with their currency code.
        4. Reference entities by their IDs when reporting results.
        5. If an operation fails, explain the error clearly and suggest corrective action.
        6. Never expose internal system details or raw exception messages to the user.
        """;

    protected override IEnumerable<AITool> GetTools(IServiceProvider serviceProvider)
    {
        // Aggregate tools from all Finance sub-domain tool classes
        return InvoiceTools.CreateAll(serviceProvider)
            .Concat(LedgerTools.CreateAll(serviceProvider))
            .Concat(PaymentTools.CreateAll(serviceProvider));
    }
}
