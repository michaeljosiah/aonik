using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for invoice / billing operations.
/// Each method is exposed to the LLM via <see cref="AIFunctionFactory.Create"/>.
/// Read-only tools are safe for autonomous use; mutating tools are wrapped with
/// <see cref="ApprovalRequiredAIFunction"/> to enforce human-in-the-loop approval.
/// </summary>
internal sealed class InvoiceTools
{
    private readonly IBillingService _billingService;

    private InvoiceTools(IBillingService billingService) => _billingService = billingService;

    [Description("Retrieves an invoice by its unique identifier. Returns the full invoice with line items, status, and totals.")]
    public async Task<InvoiceResponse?> GetInvoice(
        [Description("The unique identifier (GUID) of the invoice to retrieve")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return await _billingService.GetInvoiceAsync(invoiceId, cancellationToken);
    }

    [Description("Creates a new draft invoice for a customer with one or more line items. Returns the created invoice with its generated ID.")]
    public async Task<InvoiceResponse> CreateInvoice(
        [Description("The customer account ID to bill")] Guid customerId,
        [Description("A unique invoice number (e.g. INV-2025-001)")] string invoiceNumber,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Payment due date in UTC")] DateTime dueUtc,
        [Description("Line item descriptions")] List<CreateInvoiceLineItemRequest> lineItems,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateInvoiceRequest(customerId, invoiceNumber, currency, dueUtc, lineItems);
        return await _billingService.CreateInvoiceAsync(request, cancellationToken);
    }

    [Description("Issues a draft invoice, transitioning it to 'Issued' status so it becomes payable.")]
    public async Task<string> IssueInvoice(
        [Description("The unique identifier (GUID) of the invoice to issue")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await _billingService.IssueInvoiceAsync(invoiceId, cancellationToken);
        return $"Invoice {invoiceId} has been issued successfully.";
    }

    [Description("Cancels an invoice, transitioning it to 'Cancelled' status.")]
    public async Task<string> CancelInvoice(
        [Description("The unique identifier (GUID) of the invoice to cancel")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await _billingService.CancelInvoiceAsync(invoiceId, cancellationToken);
        return $"Invoice {invoiceId} has been cancelled.";
    }

    [Description("Marks an invoice as paid after payment has been received.")]
    public async Task<string> MarkInvoicePaid(
        [Description("The unique identifier (GUID) of the invoice to mark as paid")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await _billingService.MarkInvoiceAsPaidAsync(invoiceId, cancellationToken);
        return $"Invoice {invoiceId} has been marked as paid.";
    }

    [Description("Lists invoices, optionally filtered by status. Returns all matching invoices with line items, totals, and dates.")]
    public async Task<IReadOnlyList<InvoiceResponse>> ListInvoices(
        [Description("Optional status filter: Draft, Issued, Paid, or Cancelled. Leave empty for all.")] string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _billingService.ListInvoicesAsync(statusFilter, cancellationToken);
    }

    [Description("Adds a new line item to an existing invoice and recalculates totals.")]
    public async Task<string> AddLineToInvoice(
        [Description("The unique identifier (GUID) of the invoice")] Guid invoiceId,
        [Description("Description of the line item")] string description,
        [Description("Quantity of the item")] decimal quantity,
        [Description("Unit price of the item")] decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        var lineRequest = new CreateInvoiceLineItemRequest(description, quantity, unitPrice);
        await _billingService.AddLineToInvoiceAsync(invoiceId, lineRequest, cancellationToken);
        return $"Line item '{description}' added to invoice {invoiceId}.";
    }

    [Description("Updates the quantity of an existing invoice line item and recalculates totals.")]
    public async Task<string> UpdateLineQuantity(
        [Description("The unique identifier (GUID) of the invoice line item")] Guid invoiceLineId,
        [Description("The new quantity")] decimal quantity,
        CancellationToken cancellationToken = default)
    {
        await _billingService.UpdateLineQuantityAsync(invoiceLineId, quantity, cancellationToken);
        return $"Line item {invoiceLineId} quantity updated to {quantity}.";
    }

    [Description("Updates the unit price of an existing invoice line item and recalculates totals.")]
    public async Task<string> UpdateLineUnitPrice(
        [Description("The unique identifier (GUID) of the invoice line item")] Guid invoiceLineId,
        [Description("The new unit price")] decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        await _billingService.UpdateLineUnitPriceAsync(invoiceLineId, unitPrice, cancellationToken);
        return $"Line item {invoiceLineId} unit price updated to {unitPrice}.";
    }

    [Description("Applies a discount amount to an invoice and recalculates totals.")]
    public async Task<string> ApplyDiscount(
        [Description("The unique identifier (GUID) of the invoice")] Guid invoiceId,
        [Description("The discount amount to apply")] decimal discountTotal,
        CancellationToken cancellationToken = default)
    {
        await _billingService.ApplyDiscountAsync(invoiceId, discountTotal, cancellationToken);
        return $"Discount of {discountTotal} applied to invoice {invoiceId}.";
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all invoice tools.
    /// Mutating tools are wrapped with <see cref="ApprovalRequiredAIFunction"/>
    /// for human-in-the-loop approval.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new InvoiceTools(serviceProvider.GetRequiredService<IBillingService>());

        // Read-only — safe for autonomous use
        yield return AIFunctionFactory.Create(tools.GetInvoice, name: "finance_get_invoice");
        yield return AIFunctionFactory.Create(tools.ListInvoices, name: "finance_list_invoices");

        // Mutating — require approval before execution
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.CreateInvoice, name: "finance_create_invoice"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.IssueInvoice, name: "finance_issue_invoice"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.CancelInvoice, name: "finance_cancel_invoice"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.MarkInvoicePaid, name: "finance_mark_invoice_paid"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.AddLineToInvoice, name: "finance_add_invoice_line"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.UpdateLineQuantity, name: "finance_update_line_quantity"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.UpdateLineUnitPrice, name: "finance_update_line_unit_price"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.ApplyDiscount, name: "finance_apply_invoice_discount"));
    }
}
