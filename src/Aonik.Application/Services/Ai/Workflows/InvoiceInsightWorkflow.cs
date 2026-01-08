using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Ai;
using Aonik.Application.Services.Ai.Prompts;
using Aonik.Domain.Ai.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Ai.Workflows;

public class InvoiceInsightWorkflow
{
    private readonly IAonikDbContext _dbContext;
    private readonly IPromptStore _promptStore;
    private readonly IModelProvider _modelProvider;

    public InvoiceInsightWorkflow(
        IAonikDbContext dbContext,
        IPromptStore promptStore,
        IModelProvider modelProvider)
    {
        _dbContext = dbContext;
        _promptStore = promptStore;
        _modelProvider = modelProvider;
    }

    public async Task<InsightResponse> ExecuteAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Step 1: Load invoice data
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

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
Invoice Number: N/A
Total Amount: {invoice.Total} {invoice.Currency}
Status: {invoice.Status}
Due Date: {invoice.DueDate:yyyy-MM-dd}
Line Items Count: {invoice.Lines.Count}
";

        var userPrompt = userPromptTemplate.Replace("{{INVOICE_DATA}}", invoiceData);

        // Step 4: Call AI model
        var completion = await _modelProvider.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            cancellationToken);

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
