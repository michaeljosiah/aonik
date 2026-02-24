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
}
