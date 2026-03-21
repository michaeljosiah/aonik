using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Workflows;

/// <summary>
/// Reconciliation workflow using MAF concurrent pipeline pattern.
/// Three agents run in parallel to check different aspects of financial state:
/// 1. Ledger Check Agent — verifies ledger balances and journal entry integrity
/// 2. Payment Check Agent — verifies payment intent statuses and captures
/// 3. Invoice Check Agent — verifies invoice statuses match payment records
///
/// Results are aggregated into a unified reconciliation report.
///
/// <para><b>Advisory workflow:</b> This workflow is currently advisory-only.
/// The agents reason about reconciliation checks but have no domain tools wired,
/// so they cannot directly query ledger, payment, or invoice data.
/// To make this workflow operational, wire <c>LedgerTools</c>, <c>PaymentTools</c>,
/// and <c>InvoiceTools</c> into the respective check agents' tool sets.</para>
/// </summary>
public sealed class ReconciliationWorkflowFactory : IWorkflowFactory
{
    public const string Name = "financial-reconciliation";

    public string WorkflowName => Name;

    public AIAgent Build(IServiceProvider serviceProvider)
    {
        var chatClient = serviceProvider.GetRequiredService<IChatClient>();
        return BuildWorkflow(chatClient);
    }

    internal static AIAgent BuildWorkflow(IChatClient chatClient)
    {
        var ledgerCheckAgent = new ChatClientAgent(
            chatClient,
            name: "ledger-checker",
            instructions:
                """
                You are a ledger reconciliation specialist. When given a reconciliation request
                (e.g., a tenant ID or date range), check the ledger state:
                - Verify that all journal entries are balanced (debits = credits)
                - Check for any unposted or pending entries
                - Identify accounts with unusual balances
                - Report the total number of ledgers, accounts, and entries checked

                Format your findings as a structured report with sections for:
                LEDGER STATUS, ISSUES FOUND, and SUMMARY.
                """);

        var paymentCheckAgent = new ChatClientAgent(
            chatClient,
            name: "payment-checker",
            instructions:
                """
                You are a payment reconciliation specialist. When given a reconciliation request,
                check the payment state:
                - Verify all payment intents have reached terminal states (completed, failed, cancelled)
                - Identify any stuck or pending payments
                - Check for payments without matching orders
                - Report the total value of payments by status

                Format your findings as a structured report with sections for:
                PAYMENT STATUS, ISSUES FOUND, and SUMMARY.
                """);

        var invoiceCheckAgent = new ChatClientAgent(
            chatClient,
            name: "invoice-checker",
            instructions:
                """
                You are an invoice reconciliation specialist. When given a reconciliation request,
                check the invoice state:
                - Verify issued invoices have matching payment records
                - Identify overdue invoices
                - Check for invoices in inconsistent states
                - Report the total value of invoices by status

                Format your findings as a structured report with sections for:
                INVOICE STATUS, ISSUES FOUND, and SUMMARY.
                """);

        // Build concurrent workflow: all three agents run in parallel
        // Aggregator combines results from all agents into a single report
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            Name,
            [ledgerCheckAgent, paymentCheckAgent, invoiceCheckAgent],
            AggregateReconciliationResults);

        return workflow.AsAIAgent(
            id: Name,
            name: "Financial Reconciliation",
            description: "Runs parallel checks on ledger, payment, and invoice state and produces a unified reconciliation report");
    }

    /// <summary>
    /// Aggregates results from all concurrent reconciliation agents into
    /// a single unified report.
    /// </summary>
    private static List<ChatMessage> AggregateReconciliationResults(
        IList<List<ChatMessage>> agentResults)
    {
        var combined = new List<ChatMessage>();

        // Add a header
        combined.Add(new ChatMessage(ChatRole.Assistant,
            "=== FINANCIAL RECONCILIATION REPORT ===\n"));

        for (int i = 0; i < agentResults.Count; i++)
        {
            var agentMessages = agentResults[i];
            foreach (var msg in agentMessages)
            {
                combined.Add(msg);
            }

            if (i < agentResults.Count - 1)
            {
                combined.Add(new ChatMessage(ChatRole.Assistant, "\n---\n"));
            }
        }

        combined.Add(new ChatMessage(ChatRole.Assistant,
            "\n=== END OF RECONCILIATION REPORT ==="));

        return combined;
    }
}
