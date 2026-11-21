using System.Text.Json;
using System.Text.Json.Schema;

using Aonik.Finance.Agents;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Finance.Services.Payments;

internal sealed class InvoicePaymentIntentWorkflowAction(PaymentService payments) : IWorkflowActionHandler
{
    internal const string ActionKey = "finance.payments.create_invoice_intent";
    internal const string ToolName = "finance_create_invoice_payment_intent";
    internal const string ProposalType = "Finance.CreateInvoicePaymentIntent";
    internal static readonly JsonSerializerOptions JsonOptions = FinancialDocumentActions.JsonOptions;
    internal static readonly WorkflowActionDescriptor Action = new(ActionKey, 1, ToolName, "Payment.Create",
        new FinanceToolApprovalManifest().Classify(ToolName)!.Options,
        JsonSerializer.SerializeToElement(JsonOptions.GetJsonSchemaAsNode(typeof(InvoicePaymentIntentArguments))),
        JsonSerializer.SerializeToElement(JsonOptions.GetJsonSchemaAsNode(typeof(InvoicePaymentIntentReceipt))), ["InvoiceNotDraft"], true);
    public WorkflowActionDescriptor Descriptor => Action;
    public void ValidateArguments(JsonElement arguments)
    {
        var input = arguments.Deserialize<InvoicePaymentIntentArguments>(JsonOptions);
        if (input is null || input.InvoiceId == Guid.Empty) throw new ArgumentException("A stored invoiceId is required.");
    }
    public async Task<ToolApprovalPreview?> GetPreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => await payments.PreviewInvoicePaymentAsync(invocation, cancellationToken);
    public Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => throw new PermissionDeniedException("Payment.Create", "High payment actions execute only through the approved proposal dispatcher.");
    public Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        => payments.ReconcileInvoicePaymentAsync(invocation, cancellationToken);
}

internal sealed record InvoicePaymentIntentArguments(Guid InvoiceId);
internal sealed record InvoicePaymentIntentReceipt(Guid PaymentIntentId, Guid InvoiceId, Guid OrderId, Guid OperationId,
    Guid ApprovalRequestId, string Status, Guid? AiRunId);
