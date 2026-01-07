using Aonik.Domain.Billing;

namespace Aonik.Application.Models.Billing;

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
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateTime IssuedUtc,
    DateTime DueUtc,
    List<InvoiceLineItemResponse> LineItems);

public record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
