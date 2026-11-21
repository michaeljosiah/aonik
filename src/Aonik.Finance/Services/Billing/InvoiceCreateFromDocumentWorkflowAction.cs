using System.Text.Json;
using System.Text.Json.Schema;

using Aonik.Finance.Agents;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Finance.Services.Billing;

internal sealed class InvoiceCreateFromDocumentWorkflowAction(BillingService billing) : IWorkflowActionHandler
{
    public WorkflowActionDescriptor Descriptor { get; } = new(FinancialDocumentActions.CreateInvoice, 1, FinancialDocumentActions.CreateInvoiceTool,
        "Invoice.Create", new FinanceToolApprovalManifest().Classify(FinancialDocumentActions.CreateInvoiceTool)!.Options,
        JsonSerializer.SerializeToElement(FinancialDocumentActions.JsonOptions.GetJsonSchemaAsNode(typeof(CreateInvoiceFromDocumentArguments))),
        JsonSerializer.SerializeToElement(FinancialDocumentActions.JsonOptions.GetJsonSchemaAsNode(typeof(FinancialDocumentInvoiceReceipt))));

    public void ValidateArguments(JsonElement arguments)
    {
        var input = arguments.Deserialize<CreateInvoiceFromDocumentArguments>(FinancialDocumentActions.JsonOptions);
        if (input is null || input.ExtractionId == Guid.Empty || input.CustomerId == Guid.Empty || input.OrderId == Guid.Empty
            || input.CandidateHash is not { Length: 64 } || !input.CandidateHash.All(Uri.IsHexDigit)) throw new ArgumentException("Pin the candidate ID/hash and a known customer.");
    }
    public async Task<ToolApprovalPreview?> GetPreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        ValidateArguments(invocation.Arguments);
        return await billing.PreviewDocumentInvoiceAsync(invocation, cancellationToken);
    }
    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        ValidateArguments(invocation.Arguments);
        WorkflowActionReconciliation previous = await ReconcileAsync(invocation, cancellationToken);
        if (previous.Result is not null) return previous.Result;
        if (!previous.CanRetry) throw new FinancialDocumentException("DuplicateInvoiceEvidenceRequiresReview");
        return await billing.CreateInvoiceForWorkflowAsync(invocation, cancellationToken);
    }
    public Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => billing.ReconcileDocumentInvoiceAsync(invocation, cancellationToken);
}
