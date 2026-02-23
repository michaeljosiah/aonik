using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Models.Ai;
using Aonik.Application.Services;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Ai;

public class AiInsightsService : AdminServiceBase, IAiInsightsService
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
        await EnsurePermissionAsync(cancellationToken);
        return await _invoiceInsightWorkflow.ExecuteAsync(invoiceId, cancellationToken);
    }

    private Task EnsurePermissionAsync(CancellationToken cancellationToken)
    {
        return base.EnsurePermissionAsync("Invoice.Read", cancellationToken);
    }
}
