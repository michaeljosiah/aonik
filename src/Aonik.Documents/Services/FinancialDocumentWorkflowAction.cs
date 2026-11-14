using System.Text.Json;
using System.Text.Json.Schema;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Documents.Services;

internal sealed class FinancialDocumentWorkflowAction(FinancialDocumentService service, IWorkflowApprovalReader authority,
    ITenantProvider tenantProvider) : IWorkflowActionHandler
{
    internal static readonly ToolApprovalOptions Policy = new(ToolApprovalTier.Low, "Extract a financial document review candidate");
    public WorkflowActionDescriptor Descriptor { get; } = new(FinancialDocumentActions.Extract, 1, FinancialDocumentActions.ExtractTool,
        "Document.Extract", Policy,
        JsonSerializer.SerializeToElement(FinancialDocumentActions.JsonOptions.GetJsonSchemaAsNode(typeof(ExtractFinancialDocumentArguments))),
        JsonSerializer.SerializeToElement(FinancialDocumentActions.JsonOptions.GetJsonSchemaAsNode(typeof(FinancialDocumentExtractionReceipt))));

    public void ValidateArguments(JsonElement arguments)
    {
        var input = arguments.Deserialize<ExtractFinancialDocumentArguments>(FinancialDocumentActions.JsonOptions);
        if (input is null || input.DocumentId == Guid.Empty || input.FileId == Guid.Empty) throw new ArgumentException("Pin a document and file ID.");
    }
    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        _ = tenantProvider.GetCurrentTenantId();
        ValidateArguments(invocation.Arguments);
        var input = invocation.Arguments.Deserialize<ExtractFinancialDocumentArguments>(FinancialDocumentActions.JsonOptions)!;
        if (invocation.SubjectType != "Document" || input.DocumentId != invocation.SubjectId) throw new ArgumentException("Extraction must target the pinned document.");
        await authority.RequireActiveInvocationAsync(invocation, Descriptor.Key, Descriptor.Version, cancellationToken);
        return Receipt(await service.ExtractAsync(input, invocation.Actor, cancellationToken));
    }
    public async Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
    {
        await authority.RequireActiveInvocationAsync(invocation, Descriptor.Key, Descriptor.Version, cancellationToken);
        var candidate = await service.FindAsync(invocation.Arguments.Deserialize<ExtractFinancialDocumentArguments>(FinancialDocumentActions.JsonOptions)!, invocation.Actor, cancellationToken);
        return candidate is null ? new(null, true) : new(Receipt(candidate), false);
    }
    private static WorkflowActionResult Receipt(FinancialDocumentCandidate candidate) => new(candidate.Status == "ReadyForReview" ? "Extracted" : "NeedsManualReview",
        JsonSerializer.SerializeToElement(new FinancialDocumentExtractionReceipt(candidate.ExtractionId, candidate.DocumentId, candidate.CandidateHash,
            candidate.Status, candidate.Issues, candidate.AiRunId), FinancialDocumentActions.JsonOptions), candidate.AiRunId);
}

