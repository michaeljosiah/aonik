using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Models.Ai;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Ai;

public class AiInsightsService : IAiInsightsService
{
    private readonly InvoiceInsightWorkflow _invoiceInsightWorkflow;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AiInsightsService(
        InvoiceInsightWorkflow invoiceInsightWorkflow,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
    {
        _invoiceInsightWorkflow = invoiceInsightWorkflow;
        _permissionService = permissionService;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<InsightResponse> GenerateInvoiceInsightAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        return await _invoiceInsightWorkflow.ExecuteAsync(invoiceId, cancellationToken);
    }

    private async Task EnsurePermissionAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, "Invoice.Read", cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException("Permission Invoice.Read is required.");
        }
    }
}
