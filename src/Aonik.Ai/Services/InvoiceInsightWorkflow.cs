using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Finance.Contracts.Services.Billing;
using Microsoft.Extensions.AI;

namespace Aonik.Ai.Services;

/// <summary>
/// Workflow that generates AI insights for invoices.
/// Uses IChatClient (Microsoft.Extensions.AI) instead of the legacy IModelProvider.
/// Persists insights via AiDbContext (module-scoped).
/// </summary>
internal sealed class InvoiceInsightWorkflow
{
    private readonly AiDbContext _dbContext;
    private readonly IBillingService _billingService;
    private readonly IPromptStore _promptStore;
    private readonly IChatClient _chatClient;

    public InvoiceInsightWorkflow(
        AiDbContext dbContext,
        IBillingService billingService,
        IPromptStore promptStore,
        IChatClient chatClient)
    {
        _dbContext = dbContext;
        _billingService = billingService;
        _promptStore = promptStore;
        _chatClient = chatClient;
    }

    public async Task<InsightResponse> ExecuteAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Step 1: Load invoice data via Finance module service contract
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

        // Step 5: Create and save insight
        var insight = new Insight
        {
            Id = Guid.NewGuid(),
            SubjectType = "Invoice",
            SubjectId = invoiceId,
            Title = "Insight for Invoice",
            Summary = completion,
            CreatedUtc = DateTime.UtcNow
        };

        _dbContext.Insights.Add(insight);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InsightResponse(
            insight.Id,
            insight.SubjectType,
            insight.SubjectId,
            insight.Title,
            insight.Summary,
            DateTime.UtcNow);
    }
}
