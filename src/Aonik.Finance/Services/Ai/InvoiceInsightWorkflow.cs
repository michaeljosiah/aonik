using Aonik.Finance.Contracts.Services.Billing;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Services.Ai;

/// <summary>
/// Workflow that generates AI insights for invoices.
/// Uses IChatClient (Microsoft.Extensions.AI) and IPromptStore (SharedKernel) for AI infrastructure.
/// Persists insights via IInsightWriter (SharedKernel contract, implemented by AI module).
/// </summary>
internal sealed class InvoiceInsightWorkflow
{
    private readonly IBillingService _billingService;
    private readonly IPromptStore _promptStore;
    private readonly IChatClient _chatClient;
    private readonly IInsightWriter _insightWriter;

    public InvoiceInsightWorkflow(
        IBillingService billingService,
        IPromptStore promptStore,
        IChatClient chatClient,
        IInsightWriter insightWriter)
    {
        _billingService = billingService;
        _promptStore = promptStore;
        _chatClient = chatClient;
        _insightWriter = insightWriter;
    }

    internal static class PromptNames
    {
        public const string InvoiceInsight = "invoice_insight";
    }

    public async Task<InsightResponse> ExecuteAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Step 1: Load invoice data via Finance service contract
        var invoice = await _billingService.GetInvoiceAsync(invoiceId, cancellationToken);

        if (invoice == null)
        {
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        }

        // Step 2: Load prompts
        var systemPrompt = await _promptStore.LoadPromptAsync(
            PromptNames.InvoiceInsight,
            "v1",
            "system",
            cancellationToken);

        var userPromptTemplate = await _promptStore.LoadPromptAsync(
            PromptNames.InvoiceInsight,
            "v1",
            "user",
            cancellationToken);

        // Step 3: Build user prompt with invoice data
        var invoiceData = $@"
Invoice Number: {invoice.InvoiceNumber}
Total Amount: {invoice.TotalAmount} {invoice.Currency}
Status: {invoice.Status}
Due Date: {invoice.DueUtc:yyyy-MM-dd}
Line Items Count: {invoice.LineItems.Count}
";

        var userPrompt = userPromptTemplate.Replace("{{INVOICE_DATA}}", invoiceData);

        // Step 4: Call AI model via IChatClient
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var completion = response.Text ?? string.Empty;

        // Step 5: Persist insight via AI module's IInsightWriter
        return await _insightWriter.SaveInsightAsync(
            "Invoice",
            invoiceId,
            "Insight for Invoice",
            completion,
            cancellationToken);
    }
}
