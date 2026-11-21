using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Entities.Billing;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Finance.Services.Billing;

internal partial class BillingService
{
    internal async Task<ToolApprovalPreview> PreviewDocumentInvoiceAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken)
    {
        var prepared = await PrepareDocumentInvoiceAsync(invocation, cancellationToken);
        if (await _dbContext.Invoices.AsNoTracking().AnyAsync(invoice => invoice.TenantId == _tenantProvider.GetCurrentTenantId()
            && (invoice.IdempotencyKey == prepared.Request.IdempotencyKey || invoice.IntakeBusinessKey == prepared.BusinessKey), cancellationToken))
            throw new FinancialDocumentException("DuplicateInvoiceEvidenceRequiresReview");
        return new("Create a draft sales invoice from document evidence", null, JsonSerializer.SerializeToElement(new
        {
            prepared.Candidate.ExtractionId, prepared.Candidate.DocumentId, prepared.Candidate.FileId,
            prepared.Candidate.SourceHash, prepared.Candidate.CandidateHash, prepared.Candidate.AiRunId,
            prepared.Candidate.Extractor, prepared.Candidate.Fields.Confidence, prepared.Candidate.Fields.Evidence,
            kind = prepared.Candidate.Fields.Kind, invoice = prepared.Request,
            subtotal = prepared.Candidate.Fields.Subtotal, tax = prepared.Candidate.Fields.Tax,
            discount = prepared.Candidate.Fields.Discount, total = prepared.Candidate.Fields.Total,
        }, FinancialDocumentActions.JsonOptions));
    }

    internal async Task<WorkflowActionResult> CreateInvoiceForWorkflowAsync(WorkflowActionInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        if (_workflowApprovals is null) throw new PermissionDeniedException("Invoice.Create");
        await _workflowApprovals.RequireActiveInvocationAsync(invocation, FinancialDocumentActions.CreateInvoice, 1, cancellationToken);
        Guid reviewer = await _workflowApprovals.RequireApprovedActorAsync(invocation, FinancialDocumentActions.CreateInvoice, 1, cancellationToken);
        if (!await PermissionService.HasPermissionAsync(reviewer, "Invoice.Create", cancellationToken)
            || !await PermissionService.HasPermissionAsync(reviewer, "Customers.Read", cancellationToken)) throw new PermissionDeniedException("Invoice.Create");
        var prepared = await PrepareDocumentInvoiceAsync(invocation, cancellationToken);
        _ = await _financialDocuments!.RequireAsync(prepared.Candidate.ExtractionId, new("User", reviewer, reviewer), cancellationToken);
        var provenance = new DocumentInvoiceProvenance(prepared.Candidate.ExtractionId, prepared.Candidate.DocumentId, prepared.Candidate.FileId,
            prepared.Candidate.SourceHash, prepared.Candidate.CandidateHash, Hash(JsonSerializer.Serialize(prepared.Request, FinancialDocumentActions.JsonOptions)),
            invocation.OperationId, invocation.ApprovalRequestId!.Value, reviewer, prepared.Candidate.AiRunId);
        var creation = new WorkflowInvoiceCreation(invocation, prepared.BusinessKey, provenance, prepared.CurrencyScale);
        InvoiceResponse invoice = await CreateInvoiceAsync(prepared.Request, machineAuthorised: false, cancellationToken, creation);
        return DocumentReceipt(invoice.Id, provenance);
    }

    internal async Task<WorkflowActionReconciliation> ReconcileDocumentInvoiceAsync(WorkflowActionInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (_workflowApprovals is null) throw new PermissionDeniedException("Invoice.Create");
        await _workflowApprovals.RequireActiveInvocationAsync(invocation, FinancialDocumentActions.CreateInvoice, 1, cancellationToken);
        var prepared = await PrepareDocumentInvoiceAsync(invocation, cancellationToken);
        Invoice? invoice = await _dbContext.Invoices.AsNoTracking().SingleOrDefaultAsync(invoice => invoice.TenantId == _tenantProvider.GetCurrentTenantId()
            && invoice.IdempotencyKey == prepared.Request.IdempotencyKey, cancellationToken);
        if (invoice is null) return new(null, !await _dbContext.Invoices.AsNoTracking().AnyAsync(invoice => invoice.TenantId == _tenantProvider.GetCurrentTenantId()
            && invoice.IntakeBusinessKey == prepared.BusinessKey, cancellationToken));
        DocumentInvoiceProvenance? provenance = ReadDocumentProvenance(invoice);
        if (provenance?.OperationId != invocation.OperationId || provenance.ApprovalRequestId != invocation.ApprovalRequestId
            || provenance.ExtractionId != prepared.Candidate.ExtractionId || provenance.CandidateHash != prepared.Candidate.CandidateHash
            || provenance.PayloadHash != Hash(JsonSerializer.Serialize(prepared.Request, FinancialDocumentActions.JsonOptions))) return new(null, false);
        return new(DocumentReceipt(invoice.Id, provenance), false);
    }

    private async Task<PreparedDocumentInvoice> PrepareDocumentInvoiceAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken)
    {
        if (_financialDocuments is null) throw new FinancialDocumentException("Financial document evidence is unavailable.");
        var input = invocation.Arguments.Deserialize<CreateInvoiceFromDocumentArguments>(FinancialDocumentActions.JsonOptions)
            ?? throw new ArgumentException("Reviewed candidate arguments are required.");
        FinancialDocumentCandidate candidate = await _financialDocuments.RequireAsync(input.ExtractionId, invocation.Actor, cancellationToken);
        if (invocation.SubjectType != "Document" || invocation.SubjectId != candidate.DocumentId || candidate.CandidateHash != input.CandidateHash
            || candidate.Status != "ReadyForReview" || candidate.Fields.Kind != "SalesInvoice" || candidate.Issues.Count != 0)
            throw new FinancialDocumentException("FinancialCandidateRequiresManualReview");
        Guid tenantId = _tenantProvider.GetCurrentTenantId();
        var currency = await _dbContext.Currencies.AsNoTracking().Where(currency => currency.Code == candidate.Fields.Currency
            && (currency.TenantId == null || currency.TenantId == tenantId)).OrderByDescending(currency => currency.TenantId != null)
            .Select(currency => new { currency.IsActive, currency.MinorUnit }).FirstOrDefaultAsync(cancellationToken);
        int? scale = currency?.IsActive == true ? currency.MinorUnit : null;
        if (scale is not (>= 0 and <= 4) || candidate.AiRunId == Guid.Empty || candidate.Fields.Tax != 0 || candidate.Fields.Discount != 0
            || candidate.Fields.DueUtc is not { Kind: DateTimeKind.Utc } || candidate.Fields.Lines.Count is < 1 or > 50
            || candidate.Fields.Total <= 0 || candidate.Fields.Total != decimal.Round(candidate.Fields.Total, scale.Value)
            || candidate.Fields.Lines.Any(line => line.Quantity <= 0 || line.UnitPrice < 0
                || line.Total != decimal.Round(line.Quantity * line.UnitPrice, scale.Value, MidpointRounding.AwayFromZero))
            || candidate.Fields.Subtotal != candidate.Fields.Lines.Sum(line => line.Total) || candidate.Fields.Total != candidate.Fields.Subtotal)
            throw new FinancialDocumentException("FinancialCandidateTotalsOrCurrencyInvalid");
        CustomerAccount customer = await _dbContext.CustomerAccounts.AsNoTracking().SingleOrDefaultAsync(customer => customer.TenantId == tenantId
            && customer.Id == input.CustomerId && customer.Status == "Active", cancellationToken) ?? throw new FinancialDocumentException("FinancialCandidateCustomerUnknown");
        string? name = await _dbContext.Parties.AsNoTracking().Where(party => party.TenantId == tenantId && party.Id == customer.CustomerPartyId
            && party.Status == "Active").Select(party => party.DisplayName).SingleOrDefaultAsync(cancellationToken);
        if (name is null || !string.Equals(Normalize(name), Normalize(candidate.Fields.CounterpartyName), StringComparison.OrdinalIgnoreCase))
            throw new FinancialDocumentException("FinancialCandidateCustomerMismatch");
        if (input.OrderId is { } orderId && !await _dbContext.Orders.AsNoTracking().AnyAsync(order => order.TenantId == tenantId && order.Id == orderId
            && order.PayerPartyId == customer.CustomerPartyId && order.CurrencyIn == candidate.Fields.Currency && order.AmountIn == candidate.Fields.Total
            && order.Status != "Cancelled", cancellationToken)) throw new FinancialDocumentException("FinancialCandidateOrderMismatch");
        var request = new CreateInvoiceRequest(customer.Id, candidate.Fields.InvoiceNumber, candidate.Fields.Currency, candidate.Fields.DueUtc!.Value,
            candidate.Fields.Lines.Select(line => new CreateInvoiceLineItemRequest(line.Description, line.Quantity, line.UnitPrice)).ToList(),
            input.OrderId, "document-" + candidate.SourceHash);
        string businessKey = Hash($"{customer.Id:D}:{Normalize(candidate.Fields.InvoiceNumber).ToUpperInvariant()}:{candidate.Fields.Currency}");
        return new(candidate, request, businessKey, scale.Value);
    }

    private static void RequireSameDocumentOperation(Invoice invoice, WorkflowInvoiceCreation workflow)
    {
        DocumentInvoiceProvenance? previous = ReadDocumentProvenance(invoice);
        if (previous != workflow.Provenance) throw new FinancialDocumentException("DuplicateInvoiceEvidenceRequiresReview");
    }
    private static DocumentInvoiceProvenance? ReadDocumentProvenance(Invoice invoice)
    {
        try { return invoice.ProvenanceJson == "{}" ? null : JsonSerializer.Deserialize<DocumentInvoiceProvenance>(invoice.ProvenanceJson, FinancialDocumentActions.JsonOptions); }
        catch (JsonException) { return null; }
    }
    private static WorkflowActionResult DocumentReceipt(Guid invoiceId, DocumentInvoiceProvenance provenance) => new("Created",
        JsonSerializer.SerializeToElement(new FinancialDocumentInvoiceReceipt(invoiceId, provenance.ExtractionId, provenance.ApprovalRequestId,
            provenance.OperationId, provenance.AiRunId, "Draft"), FinancialDocumentActions.JsonOptions), provenance.AiRunId);
    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record PreparedDocumentInvoice(FinancialDocumentCandidate Candidate, CreateInvoiceRequest Request, string BusinessKey, int CurrencyScale);
    private sealed record WorkflowInvoiceCreation(WorkflowActionInvocation Invocation, string BusinessKey, DocumentInvoiceProvenance Provenance, int CurrencyScale);
    private sealed record DocumentInvoiceProvenance(Guid ExtractionId, Guid DocumentId, Guid FileId, string SourceHash, string CandidateHash,
        string PayloadHash, Guid OperationId, Guid ApprovalRequestId, Guid ReviewerId, Guid AiRunId);
}
