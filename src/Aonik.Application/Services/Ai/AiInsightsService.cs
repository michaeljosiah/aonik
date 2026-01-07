using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Ai;
using Aonik.Application.Services.Ai.Workflows;

namespace Aonik.Application.Services.Ai;

public class AiInsightsService : IAiInsightsService
{
    private readonly InvoiceInsightWorkflow _invoiceInsightWorkflow;

    public AiInsightsService(InvoiceInsightWorkflow invoiceInsightWorkflow)
    {
        _invoiceInsightWorkflow = invoiceInsightWorkflow;
    }

    public async Task<InsightResponse> GenerateInvoiceInsightAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        return await _invoiceInsightWorkflow.ExecuteAsync(invoiceId, cancellationToken);
    }
}
