using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Ai.Services;

internal sealed class AiInsightsService : AiServiceBase, IAiInsightsService
{
    private readonly InvoiceInsightWorkflow _invoiceInsightWorkflow;

    public AiInsightsService(
        InvoiceInsightWorkflow invoiceInsightWorkflow,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
        : base(currentUserProvider, permissionService)
    {
        _invoiceInsightWorkflow = invoiceInsightWorkflow;
    }

    public async Task<InsightResponse> GenerateInvoiceInsightAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Read", cancellationToken);
        return await _invoiceInsightWorkflow.ExecuteAsync(invoiceId, cancellationToken);
    }
}
