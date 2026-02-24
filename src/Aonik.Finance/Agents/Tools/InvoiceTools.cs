using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for invoice / billing operations.
/// Each method is exposed to the LLM via <see cref="AIFunctionFactory.Create"/>.
/// Read-only tools are safe for autonomous use; mutating tools should go through
/// the proposal pattern at the agent level.
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

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all invoice tools.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new InvoiceTools(serviceProvider.GetRequiredService<IBillingService>());

        yield return AIFunctionFactory.Create(tools.GetInvoice, name: "finance_get_invoice");
        yield return AIFunctionFactory.Create(tools.CreateInvoice, name: "finance_create_invoice");
        yield return AIFunctionFactory.Create(tools.IssueInvoice, name: "finance_issue_invoice");
        yield return AIFunctionFactory.Create(tools.CancelInvoice, name: "finance_cancel_invoice");
        yield return AIFunctionFactory.Create(tools.MarkInvoicePaid, name: "finance_mark_invoice_paid");
    }
}
