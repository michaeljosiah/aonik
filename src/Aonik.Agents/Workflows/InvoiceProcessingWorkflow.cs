using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Workflows;

/// <summary>
/// Invoice processing workflow using MAF sequential pipeline pattern.
/// Steps:
/// 1. Validation Agent — checks invoice data completeness and correctness
/// 2. Processing Agent — creates the invoice and posts to ledger
/// 3. Notification Agent — confirms completion and summarises the result
///
/// This workflow is exposed as an <see cref="AIAgent"/> that can be invoked
/// directly or composed as a tool for the master orchestrator.
/// </summary>
public static class InvoiceProcessingWorkflow
{
    public const string WorkflowName = "invoice-processing";

    /// <summary>
    /// Builds the sequential workflow as a single <see cref="AIAgent"/>
    /// that processes invoice creation requests through a validation,
    /// processing, and notification pipeline.
    /// </summary>
    public static AIAgent Build(IChatClient chatClient)
    {
        var validationAgent = new ChatClientAgent(
            chatClient,
            name: "invoice-validator",
            instructions:
                """
                You are an invoice validation specialist. When given an invoice creation request,
                verify that all required fields are present and valid:
                - Customer account ID must be provided
                - Currency must be a valid ISO 4217 code
                - Due date must be in the future
                - At least one line item is required
                - Each line item must have a description, quantity > 0, and unit price >= 0

                If validation passes, respond with "VALIDATED" followed by a summary of the invoice.
                If validation fails, list all issues that need to be corrected.
                """);

        var processingAgent = new ChatClientAgent(
            chatClient,
            name: "invoice-processor",
            instructions:
                """
                You are an invoice processing agent. When you receive a validated invoice request,
                confirm that it has been validated (look for "VALIDATED" in the previous message).

                If validated:
                - Summarise the invoice that would be created (customer, currency, line items, total)
                - Confirm the invoice should be created as a draft
                - Note that ledger entries will be posted when the invoice is issued

                If not validated, instruct the user to fix the validation issues first.
                """);

        var notificationAgent = new ChatClientAgent(
            chatClient,
            name: "invoice-notifier",
            instructions:
                """
                You are a notification agent. Summarise the entire invoice processing result
                in a clear, user-friendly format:
                - State whether the invoice was successfully processed or had issues
                - Include key details: invoice number/ID, customer, total amount, status
                - Suggest next steps (e.g., "issue the invoice when ready to send to customer")
                """);

        // Build sequential pipeline: validate -> process -> notify
        var workflow = AgentWorkflowBuilder.BuildSequential(
            WorkflowName,
            [validationAgent, processingAgent, notificationAgent]);

        return workflow.AsAIAgent(
            id: WorkflowName,
            name: "Invoice Processing Pipeline",
            description: "Validates, processes, and confirms invoice creation through a multi-step pipeline");
    }
}
