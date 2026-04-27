using Aonik.Finance.Entities.Billing;

namespace Aonik.Finance.Contracts.Models.Billing;

public record CreateInvoiceRequest(
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    DateTime DueUtc,
    List<CreateInvoiceLineItemRequest> LineItems);

public record CreateInvoiceLineItemRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public record InvoiceResponse(
    Guid Id,
    /// <summary>The CustomerAccount FK on the invoice. Kept for back-compat
    /// with existing consumers; new code should prefer CustomerPartyId.</summary>
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateTime IssuedUtc,
    DateTime DueUtc,
    List<InvoiceLineItemResponse> LineItems,
    /// <summary>Party.Id of the customer this invoice is billed to,
    /// resolved via Invoice.CustomerAccountId → CustomerAccount.CustomerPartyId.
    /// Null only when the lookup couldn't resolve (orphaned account).</summary>
    Guid? CustomerPartyId = null,
    /// <summary>Party.DisplayName of the customer. Empty string when the
    /// party row is missing.</summary>
    string CustomerName = "");

public record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
