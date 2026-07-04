using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using ModelContextProtocol.Server;

namespace Aonik.Finance.Mcp.Tools;

/// <summary>
/// MCP tools for invoice and billing operations.
/// Domain services are injected via DI into method parameters.
/// </summary>
[McpServerToolType]
public static class InvoiceMcpTools
{
    [McpServerTool(Name = "finance_get_invoice"), Description("Retrieves an invoice by its unique identifier. Returns the full invoice with line items, status, and totals.")]
    public static async Task<InvoiceResponse?> GetInvoice(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice to retrieve")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return await billingService.GetInvoiceAsync(invoiceId, cancellationToken);
    }

    [McpServerTool(Name = "finance_create_invoice"), Description("Creates a new draft invoice for a customer with one or more line items. Returns the created invoice with its generated ID.")]
    public static async Task<InvoiceResponse> CreateInvoice(
        IBillingService billingService,
        [Description("The customer account ID to bill")] Guid customerId,
        [Description("A unique invoice number (e.g. INV-2025-001)")] string invoiceNumber,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Payment due date in UTC")] DateTime dueUtc,
        [Description("Line item descriptions")] List<CreateInvoiceLineItemRequest> lineItems,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateInvoiceRequest(customerId, invoiceNumber, currency, dueUtc, lineItems);
        return await billingService.CreateInvoiceAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "finance_issue_invoice"), Description("Issues a draft invoice, transitioning it to 'Issued' status so it becomes payable.")]
    public static async Task<string> IssueInvoice(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice to issue")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await billingService.IssueInvoiceAsync(invoiceId, cancellationToken);
        return $"Invoice {invoiceId} has been issued successfully.";
    }

    [McpServerTool(Name = "finance_cancel_invoice"), Description("Cancels an invoice, transitioning it to 'Cancelled' status.")]
    public static async Task<string> CancelInvoice(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice to cancel")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await billingService.CancelInvoiceAsync(invoiceId, cancellationToken);
        return $"Invoice {invoiceId} has been cancelled.";
    }

    [McpServerTool(Name = "finance_mark_invoice_paid"), Description("Marks an invoice as paid after payment has been received.")]
    public static async Task<string> MarkInvoicePaid(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice to mark as paid")] Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await billingService.MarkInvoiceAsPaidAsync(invoiceId, cancellationToken);
        return $"Invoice {invoiceId} has been marked as paid.";
    }

    [McpServerTool(Name = "finance_list_invoices"), Description("Lists invoices, optionally filtered by status. Returns the most recent invoices (up to a server-side page limit), each with line items, totals, and dates — not necessarily every invoice.")]
    public static async Task<IReadOnlyList<InvoiceResponse>> ListInvoices(
        IBillingService billingService,
        [Description("Optional status filter: Draft, Issued, Paid, or Cancelled. Leave empty for all.")] string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await billingService.ListInvoicesAsync(statusFilter, cancellationToken: cancellationToken);
    }

    [McpServerTool(Name = "finance_add_invoice_line"), Description("Adds a new line item to an existing invoice and recalculates totals.")]
    public static async Task<string> AddLineToInvoice(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice")] Guid invoiceId,
        [Description("Description of the line item")] string description,
        [Description("Quantity of the item")] decimal quantity,
        [Description("Unit price of the item")] decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        var lineRequest = new CreateInvoiceLineItemRequest(description, quantity, unitPrice);
        await billingService.AddLineToInvoiceAsync(invoiceId, lineRequest, cancellationToken);
        return $"Line item '{description}' added to invoice {invoiceId}.";
    }

    [McpServerTool(Name = "finance_update_line_quantity"), Description("Updates the quantity of an existing invoice line item and recalculates totals.")]
    public static async Task<string> UpdateLineQuantity(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice line item")] Guid invoiceLineId,
        [Description("The new quantity")] decimal quantity,
        CancellationToken cancellationToken = default)
    {
        await billingService.UpdateLineQuantityAsync(invoiceLineId, quantity, cancellationToken);
        return $"Line item {invoiceLineId} quantity updated to {quantity}.";
    }

    [McpServerTool(Name = "finance_update_line_unit_price"), Description("Updates the unit price of an existing invoice line item and recalculates totals.")]
    public static async Task<string> UpdateLineUnitPrice(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice line item")] Guid invoiceLineId,
        [Description("The new unit price")] decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        await billingService.UpdateLineUnitPriceAsync(invoiceLineId, unitPrice, cancellationToken);
        return $"Line item {invoiceLineId} unit price updated to {unitPrice}.";
    }

    [McpServerTool(Name = "finance_apply_invoice_discount"), Description("Applies a discount amount to an invoice and recalculates totals.")]
    public static async Task<string> ApplyDiscount(
        IBillingService billingService,
        [Description("The unique identifier (GUID) of the invoice")] Guid invoiceId,
        [Description("The discount amount to apply")] decimal discountTotal,
        CancellationToken cancellationToken = default)
    {
        await billingService.ApplyDiscountAsync(invoiceId, discountTotal, cancellationToken);
        return $"Discount of {discountTotal} applied to invoice {invoiceId}.";
    }
}
