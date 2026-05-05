using Aonik.Finance.Contracts.Services.Ai;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Finance.Services.Ai;

/// <summary>
/// Finance-specific AI insights service.
/// Delegates to domain-specific workflows for generating insights.
/// </summary>
internal sealed class FinanceInsightsService : IFinanceInsightsService
{
    private readonly InvoiceInsightWorkflow _invoiceInsightWorkflow;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FinanceInsightsService(
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
        await EnsurePermissionAsync("Invoice.Read", cancellationToken);
        return await _invoiceInsightWorkflow.ExecuteAsync(invoiceId, cancellationToken);
    }

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new PermissionDeniedException(permissionKey, "Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new PermissionDeniedException(permissionKey);
        }
    }
}
