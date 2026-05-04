using Aonik.Finance.Contracts.Services.Billing;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Services.Ai;

/// <summary>
/// Workflow that generates AI insights for invoices.
/// Uses IAiTaskProfileResolver for centralized model + prompt resolution.
/// Persists insights via IInsightWriter (SharedKernel contract, implemented by AI module).
/// </summary>
internal sealed class InvoiceInsightWorkflow
{
    private readonly IBillingService _billingService;
    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly IChatClient _chatClient;
    private readonly IInsightWriter _insightWriter;

    private const string UseCase = "invoice-insight";
    private const string PromptName = "invoice_insight";

    public InvoiceInsightWorkflow(
        IBillingService billingService,
        IAiTaskProfileResolver profileResolver,
        IChatClient chatClient,
        IInsightWriter insightWriter)
    {
        _billingService = billingService;
        _profileResolver = profileResolver;
        _chatClient = chatClient;
        _insightWriter = insightWriter;
    }

    public async Task<InsightResponse> ExecuteAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Step 1: Load invoice data via Finance service contract
        var invoice = await _billingService.GetInvoiceAsync(invoiceId, cancellationToken);

        if (invoice == null)
        {
            throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
        }

        // Step 2: Resolve AI task profile (model + prompts)
        var profile = await _profileResolver.ResolveAsync(UseCase, PromptName, cancellationToken: cancellationToken);

        // Step 3: Build user prompt with invoice data
        var invoiceData = $@"
Invoice Number: {invoice.InvoiceNumber}
Total Amount: {invoice.TotalAmount} {invoice.Currency}
Status: {invoice.Status}
Due Date: {invoice.DueUtc:yyyy-MM-dd}
Line Items Count: {invoice.LineItems.Count}
";

        var userPrompt = (profile.UserPromptTemplate ?? "{{INVOICE_DATA}}")
            .Replace("{{INVOICE_DATA}}", invoiceData);

        // Step 4: Call AI model via IChatClient
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(profile.SystemPrompt))
            messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
        messages.Add(new ChatMessage(ChatRole.User, userPrompt));

        var options = new ChatOptions
        {
            ModelId = profile.ModelId,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                // Stamp the use_case so worker / API traces label this
                // run as "invoice-insight" instead of the generic "chat".
                [AiTelemetry.UseCaseAttribute] = UseCase,
            },
        };
        var response = await _chatClient.GetResponseAsync(messages, options: options, cancellationToken: cancellationToken);
        var completion = response.Text ?? string.Empty;

        // Step 5: Persist insight via AI module's IInsightWriter
        return await _insightWriter.SaveInsightAsync(
            "Invoice",
            invoiceId,
            "Insight for Invoice",
            completion,
            cancellationToken: cancellationToken);
    }
}
