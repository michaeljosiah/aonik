using Aonik.Application.Models.Ai;

namespace Aonik.Application.Services.Ai;

public interface IAiInsightsService
{
    Task<InsightResponse> GenerateInvoiceInsightAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
