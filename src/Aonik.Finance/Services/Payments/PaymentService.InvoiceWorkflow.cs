using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Finance.Services.Payments;

internal partial class PaymentService
{
    internal async Task<ToolApprovalPreview> PreviewInvoicePaymentAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken)
    {
        var prepared = await PrepareInvoicePaymentAsync(invocation, cancellationToken);
        if (await _dbContext.PaymentIntents.AsNoTracking().AnyAsync(intent => intent.TenantId == _tenantProvider.GetCurrentTenantId()
            && (intent.IdempotencyKey == IntentKey(invocation.SubjectId) || (intent.InvoiceId == invocation.SubjectId
                && intent.Status != "Cancelled" && intent.Status != "Failed")), cancellationToken))
            throw new InvalidStateException("An invoice payment intent already exists. Review its outcome before proposing another.");
        return PaymentPreview(prepared);
    }

    internal async Task<WorkflowPaymentReceipt> CreateInvoicePaymentFromProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        if (_workflowApprovals is null) throw new PermissionDeniedException("Payment.Create");
        ApprovedWorkflowProposal approval = await _workflowApprovals.RequireApprovedProposalAsync(proposalId, InvoicePaymentIntentWorkflowAction.Action, cancellationToken);
        WorkflowActionInvocation invocation = approval.Invocation;
        if (!await PermissionService.HasPermissionAsync(approval.ReviewerId, "Payment.Create", cancellationToken)
            || !await PermissionService.HasPermissionAsync(approval.ReviewerId, "Orders.Read", cancellationToken)) throw new PermissionDeniedException("Payment.Create");
        var prepared = await PrepareInvoicePaymentAsync(invocation, cancellationToken);
        _ = await _invoiceFacts!.LoadAsync(invocation.SubjectId, new("User", approval.ReviewerId, approval.ReviewerId), cancellationToken);
        ToolApprovalPreview current = PaymentPreview(prepared);
        if (Hash(current) != Hash(approval.Preview)) throw new InvalidStateException("The reviewed Order or invoice changed. Prepare a new proposal.");
        var provenance = new InvoicePaymentProvenance(invocation.RunId, invocation.StepRunId, invocation.OperationId, invocation.ApprovalRequestId!.Value,
            proposalId, invocation.SubjectId, prepared.Request.OrderId, prepared.Request.Amount, prepared.Request.Currency,
            prepared.Request.PayerPartyId!.Value, prepared.PayeePartyId, approval.ReviewerId, Hash(invocation.Arguments), prepared.InvoiceHash,
            prepared.OrderHash, prepared.AiRunId ?? approval.AiRunId);
        string key = IntentKey(invocation.SubjectId);
        PaymentIntent? previous = await _dbContext.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(intent => intent.TenantId == _tenantProvider.GetCurrentTenantId()
            && intent.IdempotencyKey == key, cancellationToken);
        if (previous is not null)
        {
            if (ReadIntentProvenance(previous) != provenance) throw new InvalidStateException("A different payment operation already owns this invoice intent.");
            return PaymentReceipt(previous, provenance);
        }
        if (await _dbContext.PaymentIntents.AsNoTracking().AnyAsync(intent => intent.TenantId == _tenantProvider.GetCurrentTenantId()
            && intent.InvoiceId == invocation.SubjectId && intent.Status != "Cancelled" && intent.Status != "Failed", cancellationToken))
            throw new InvalidStateException("An invoice payment intent already exists.");
        var workflow = new WorkflowInvoicePayment(key, provenance);
        PaymentIntentResponse created = await CreatePaymentIntentCoreAsync(prepared.Request, workflow, cancellationToken);
        return PaymentReceipt(await _dbContext.PaymentIntents.AsNoTracking().SingleAsync(intent => intent.TenantId == _tenantProvider.GetCurrentTenantId()
            && intent.Id == created.Id, cancellationToken), provenance);
    }

    internal async Task<WorkflowActionReconciliation> ReconcileInvoicePaymentAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken)
    {
        ValidateInvoiceInvocation(invocation);
        if (_invoiceFacts is null) throw new PermissionDeniedException("Invoice.Read");
        _ = await _invoiceFacts.LoadAsync(invocation.SubjectId, invocation.Actor, cancellationToken) ?? throw new NotFoundException("Invoice not found.");
        PaymentIntent? intent = await _dbContext.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(intent => intent.TenantId == _tenantProvider.GetCurrentTenantId()
            && intent.IdempotencyKey == IntentKey(invocation.SubjectId), cancellationToken);
        InvoicePaymentProvenance? proof = intent is null ? null : ReadIntentProvenance(intent);
        if (intent is null || proof is null || proof.OperationId != invocation.OperationId || proof.ApprovalRequestId != invocation.ApprovalRequestId
            || proof.RunId != invocation.RunId || proof.StepRunId != invocation.StepRunId || proof.InvoiceId != invocation.SubjectId
            || proof.ArgumentsHash != Hash(invocation.Arguments) || proof.OrderId != intent.OrderId || proof.InvoiceId != intent.InvoiceId
            || proof.Amount != intent.Amount || proof.Currency != intent.Currency || proof.PayerPartyId != intent.PayerPartyId || proof.PayeePartyId != intent.PayeePartyId)
            return new(null, false);
        return new(PaymentReceipt(intent, proof).Result, false);
    }

    private async Task<PreparedInvoicePayment> PrepareInvoicePaymentAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken)
    {
        ValidateInvoiceInvocation(invocation);
        if (_invoiceFacts is null) throw new PermissionDeniedException("Invoice.Read");
        WorkflowFactsSnapshot facts = await _invoiceFacts.LoadAsync(invocation.SubjectId, invocation.Actor, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");
        if (facts.SubjectVersion != invocation.ExpectedSubjectVersion || WorkflowFactsHash.Compute(facts) != invocation.FactsHash)
            throw new InvalidStateException("The workflow invoice changed after preparation.");
        Guid tenantId = _tenantProvider.GetCurrentTenantId();
        Invoice invoice = await _dbContext.Invoices.AsNoTracking().SingleAsync(invoice => invoice.TenantId == tenantId && invoice.Id == invocation.SubjectId, cancellationToken);
        if (invoice.Status != "Issued" || invoice.OrderId is not { } orderId || invoice.Total <= 0)
            throw new InvalidStateException("Payment initiation requires an issued invoice linked to a stored Order.");
        Order order = await _dbContext.Orders.AsNoTracking().SingleOrDefaultAsync(order => order.TenantId == tenantId && order.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Invoice Order not found.");
        CustomerAccount customer = await _dbContext.CustomerAccounts.AsNoTracking().SingleOrDefaultAsync(customer => customer.TenantId == tenantId
            && customer.Id == invoice.CustomerAccountId && customer.Status == "Active", cancellationToken) ?? throw new InvalidStateException("The invoice customer is unavailable.");
        Guid[] parties = await _dbContext.Parties.AsNoTracking().Where(party => party.TenantId == tenantId && party.Status == "Active"
            && (party.Id == customer.CustomerPartyId || party.Id == customer.MerchantPartyId)).Select(party => party.Id).ToArrayAsync(cancellationToken);
        var currency = await _dbContext.Currencies.AsNoTracking().Where(currency => currency.Code == invoice.Currency && (currency.TenantId == null || currency.TenantId == tenantId))
            .OrderByDescending(currency => currency.TenantId != null).Select(currency => new { currency.IsActive, currency.MinorUnit }).FirstOrDefaultAsync(cancellationToken);
        int? scale = currency?.IsActive == true ? currency.MinorUnit : null;
        if (scale is not (>= 0 and <= 4) || invoice.Total != decimal.Round(invoice.Total, scale.Value)
            || order.Status is not ("Draft" or "PendingFunding" or "Approved") || order.AmountIn != invoice.Total || order.CurrencyIn != invoice.Currency
            || order.PayerPartyId != customer.CustomerPartyId || customer.CustomerPartyId == Guid.Empty || customer.MerchantPartyId == Guid.Empty
            || !parties.Contains(customer.CustomerPartyId) || !parties.Contains(customer.MerchantPartyId))
            throw new InvalidStateException("The invoice amount, currency, parties or Order intent no longer match.");
        Guid? aiRunId = null; string? sourceHash = null; string? candidateHash = null;
        if (invoice.IntakeBusinessKey is not null)
        {
            using JsonDocument evidence = JsonDocument.Parse(invoice.ProvenanceJson);
            if (_financialDocuments is null || !evidence.RootElement.TryGetProperty("extractionId", out JsonElement extraction) || !extraction.TryGetGuid(out Guid id))
                throw new PermissionDeniedException("Document.Read");
            FinancialDocumentCandidate candidate = await _financialDocuments.RequireContinuationAsync(id, invocation.Actor, cancellationToken);
            aiRunId = candidate.AiRunId; sourceHash = candidate.SourceHash; candidateHash = candidate.CandidateHash;
        }
        return new(new(invoice.Total, invoice.Currency, "", order.Id, invoice.Id, customer.CustomerPartyId), customer.MerchantPartyId,
            InvoiceHash(invoice), OrderHash(order), order.Status, aiRunId, sourceHash, candidateHash);
    }

    private async Task FenceInvoicePaymentAsync(Order order, WorkflowInvoicePayment workflow, CancellationToken cancellationToken)
    {
        await _dbContext.Entry(order).ReloadAsync(cancellationToken);
        Invoice invoice = await _dbContext.Invoices.SingleAsync(invoice => invoice.TenantId == _tenantProvider.GetCurrentTenantId()
            && invoice.Id == workflow.Provenance.InvoiceId, cancellationToken);
        await _dbContext.Entry(invoice).ReloadAsync(cancellationToken);
        if (OrderHash(order) != workflow.Provenance.OrderHash || InvoiceHash(invoice) != workflow.Provenance.InvoiceHash)
            throw new InvalidStateException("The approved invoice or Order changed before intent creation.");
        _ = await _workflowApprovals!.RequireApprovedProposalAsync(workflow.Provenance.ProposalId, InvoicePaymentIntentWorkflowAction.Action, cancellationToken);
        // Native rowversions fence a concurrent invoice/Order transition in the same save as the intent receipt.
        _dbContext.Entry(order).Property(value => value.Status).IsModified = true;
        _dbContext.Entry(invoice).Property(value => value.Status).IsModified = true;
    }

    private static void ValidateInvoiceInvocation(WorkflowActionInvocation invocation)
    {
        var input = invocation.Arguments.Deserialize<InvoicePaymentIntentArguments>(InvoicePaymentIntentWorkflowAction.JsonOptions);
        if (input is null || input.InvoiceId == Guid.Empty || invocation.SubjectType != "Invoice" || invocation.SubjectId != input.InvoiceId)
            throw new ArgumentException("Payment actions must target the workflow's pinned invoice.");
    }
    private static ToolApprovalPreview PaymentPreview(PreparedInvoicePayment prepared) => new("Create an Order-linked pending invoice payment intent", null,
        JsonSerializer.SerializeToElement(new { effect = "CreatePendingIntent", invoiceId = prepared.Request.InvoiceId, orderId = prepared.Request.OrderId,
            prepared.Request.Amount, prepared.Request.Currency, prepared.Request.PayerPartyId, prepared.PayeePartyId,
            invoiceVersion = prepared.InvoiceHash, orderVersion = prepared.OrderHash, orderStatus = prepared.OrderStatus,
            prepared.AiRunId, prepared.SourceHash, prepared.CandidateHash }, InvoicePaymentIntentWorkflowAction.JsonOptions));
    private static WorkflowPaymentReceipt PaymentReceipt(PaymentIntent intent, InvoicePaymentProvenance proof) => new(intent.Id,
        new("Created", JsonSerializer.SerializeToElement(new InvoicePaymentIntentReceipt(intent.Id, proof.InvoiceId, intent.OrderId, proof.OperationId,
            proof.ApprovalRequestId, intent.Status, proof.AiRunId), InvoicePaymentIntentWorkflowAction.JsonOptions), proof.AiRunId));
    private static InvoicePaymentProvenance? ReadIntentProvenance(PaymentIntent intent)
    {
        try { return intent.ProvenanceJson is null ? null : JsonSerializer.Deserialize<InvoicePaymentProvenance>(intent.ProvenanceJson, InvoicePaymentIntentWorkflowAction.JsonOptions); }
        catch (JsonException) { return null; }
    }
    private static string IntentKey(Guid invoiceId) => "workflow-invoice-intent-v1-" + invoiceId.ToString("N");
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, InvoicePaymentIntentWorkflowAction.JsonOptions)));
    private static string InvoiceHash(Invoice invoice) => Hash(new { invoice.Id, invoice.OrderId, invoice.CustomerAccountId, invoice.Status, invoice.Total,
        invoice.Currency, invoice.Subtotal, invoice.TaxTotal, invoice.DiscountTotal, invoice.ProvenanceJson, rowVersion = Convert.ToBase64String(invoice.RowVersion) });
    private static string OrderHash(Order order) => Hash(new { order.Id, order.OrderType, order.PayerPartyId, order.PurposeCode, order.AmountIn, order.CurrencyIn,
        order.AmountOut, order.CurrencyOut, order.FeesJson, order.FxQuoteId, order.Status, rowVersion = Convert.ToBase64String(order.RowVersion) });
    private sealed record PreparedInvoicePayment(CreatePaymentIntentRequest Request, Guid PayeePartyId, string InvoiceHash, string OrderHash,
        string OrderStatus, Guid? AiRunId, string? SourceHash, string? CandidateHash);
    private sealed record WorkflowInvoicePayment(string IdempotencyKey, InvoicePaymentProvenance Provenance);
    private sealed record InvoicePaymentProvenance(Guid RunId, Guid StepRunId, Guid OperationId, Guid ApprovalRequestId, Guid ProposalId, Guid InvoiceId,
        Guid OrderId, decimal Amount, string Currency, Guid PayerPartyId, Guid PayeePartyId, Guid ReviewerId, string ArgumentsHash,
        string InvoiceHash, string OrderHash, Guid? AiRunId);
}

internal sealed record WorkflowPaymentReceipt(Guid PaymentIntentId, WorkflowActionResult Result);
