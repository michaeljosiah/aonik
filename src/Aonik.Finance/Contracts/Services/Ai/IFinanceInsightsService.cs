using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Finance.Contracts.Services.Ai;

/// <summary>
/// Finance-specific AI insights service.
/// Generates AI-powered insights for Finance domain entities (invoices, etc.).
/// </summary>
public interface IFinanceInsightsService
{
    Task<InsightResponse> GenerateInvoiceInsightAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
