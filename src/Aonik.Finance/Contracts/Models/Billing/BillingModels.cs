using Aonik.Finance.Entities.Billing;

namespace Aonik.Finance.Contracts.Models.Billing;

public record CreateInvoiceRequest(
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    DateTime DueUtc,
    List<CreateInvoiceLineItemRequest> LineItems,
    /// <summary>
    /// The order this invoice bills for (Spec 088 §7). Optional: a standalone invoice raised
    /// without an order legitimately has none, and persists null.
    ///
    /// Load-bearing when set — order-type-aware settlement routing (§9) reads the funding order's
    /// type through this link, and invoice idempotency (§8) keys on it. Before this existed the
    /// column was never written, so both were unimplementable.
    /// </summary>
    Guid? OrderId = null,
    /// <summary>
    /// Optional idempotency key (Spec 088 §8). Unique per tenant when present; re-issuing the same
    /// key returns the original invoice instead of creating a second.
    /// </summary>
    string? IdempotencyKey = null);

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
    string CustomerName = "",
    /// <summary>The order this invoice bills for, or null for a standalone invoice (Spec 088 §7).</summary>
    Guid? OrderId = null);

public record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
