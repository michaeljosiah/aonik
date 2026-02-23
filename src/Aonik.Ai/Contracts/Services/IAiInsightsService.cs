using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

public interface IAiInsightsService
{
    Task<InsightResponse> GenerateInvoiceInsightAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
