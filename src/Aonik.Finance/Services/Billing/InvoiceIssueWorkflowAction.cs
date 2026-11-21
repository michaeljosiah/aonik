using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Agents;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Finance.Services.Billing;

internal sealed class InvoiceIssueWorkflowAction(
    BillingService billing, FinanceDbContext database, ITenantProvider tenantProvider, InvoiceWorkflowFactsProvider factsProvider)
    : IWorkflowActionHandler
{
    internal const string ActionKey = "finance.invoices.issue";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };
    private static readonly WorkflowActionDescriptor Action = new(ActionKey, 1, "finance_issue_invoice", "Invoice.Issue",
        new FinanceToolApprovalManifest().Classify("finance_issue_invoice")!.Options,
        JsonSerializer.SerializeToElement(JsonOptions.GetJsonSchemaAsNode(typeof(IssueArguments))),
        JsonSerializer.SerializeToElement(JsonOptions.GetJsonSchemaAsNode(typeof(IssueReceipt))));
    public WorkflowActionDescriptor Descriptor => Action;

    public void ValidateArguments(JsonElement arguments) => _ = ReadInvoiceId(arguments);

    public async Task<ToolApprovalPreview?> GetPreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        RequirePinnedInvoice(invocation);
        WorkflowFactsSnapshot? current = await factsProvider.LoadAsync(invocation.SubjectId, invocation.Actor, cancellationToken);
        if (current is null || current.BlockReason is not null || current.SubjectVersion != invocation.ExpectedSubjectVersion
            || WorkflowFactsHash.Compute(current) != invocation.FactsHash)
            throw new InvalidStateException("The invoice changed after the workflow action was prepared.");

        var before = new
        {
            invoiceId = invocation.SubjectId,
            status = current.Facts["invoice.status"].GetString(),
            total = current.Facts["invoice.total"].GetString(),
            currency = current.Facts["invoice.currency"].GetString(),
        };
        return new($"Issue invoice for {before.total} {before.currency}", JsonSerializer.SerializeToElement(before, JsonOptions),
            JsonSerializer.SerializeToElement(before with { status = "Issued" }, JsonOptions));
    }

    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        RequirePinnedInvoice(invocation);
        WorkflowActionReconciliation previous = await ReconcileAsync(invocation, cancellationToken);
        if (previous.Result is not null) return previous.Result;
        if (!previous.CanRetry) throw new InvalidStateException("The invoice operation requires reconciliation before it can proceed.");
        return await billing.IssueInvoiceForWorkflowAsync(invocation, cancellationToken);
    }

    public async Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        WorkflowFactsSnapshot? current = await factsProvider.LoadAsync(invocation.SubjectId, invocation.Actor, cancellationToken);
        if (current is null) return new(null, false);
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        Invoice invoice = await database.Invoices.AsNoTracking().SingleAsync(invoice => invoice.TenantId == tenantId && invoice.Id == invocation.SubjectId, cancellationToken);
        if (invoice.IssueOperationId == invocation.OperationId && invoice.IssueApprovalRequestId == invocation.ApprovalRequestId)
            return new(Receipt(invoice), false);
        return new(null, invoice.Status == "Draft" && invoice.IssueOperationId is null && current.BlockReason is null
            && current.SubjectVersion == invocation.ExpectedSubjectVersion && WorkflowFactsHash.Compute(current) == invocation.FactsHash);
    }

    internal static WorkflowActionResult Receipt(Invoice invoice) => new("Issued", JsonSerializer.SerializeToElement(
        new IssueReceipt(invoice.Id, invoice.IssueOperationId!.Value, invoice.IssueApprovalRequestId!.Value, "Issued"), JsonOptions));

    private static Guid ReadInvoiceId(JsonElement arguments)
    {
        IssueArguments? input = arguments.Deserialize<IssueArguments>(JsonOptions);
        if (input is null || input.InvoiceId == Guid.Empty) throw new ArgumentException("A valid invoiceId is required.");
        return input.InvoiceId;
    }

    private static void RequirePinnedInvoice(WorkflowActionInvocation invocation)
    {
        if (invocation.SubjectType != "Invoice" || ReadInvoiceId(invocation.Arguments) != invocation.SubjectId)
            throw new ArgumentException("Invoice actions must target the run's pinned subject.");
    }

    private sealed record IssueArguments(Guid InvoiceId);
    private sealed record IssueReceipt(Guid InvoiceId, Guid OperationId, Guid ApprovalRequestId, string Status);
}
