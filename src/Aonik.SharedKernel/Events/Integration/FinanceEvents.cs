namespace Aonik.SharedKernel.Events.Integration;

// ── Finance-originated integration events ───────────────────────────────────
// Published by the Finance module. Other modules (Platform, AI, etc.)
// subscribe to these to react to financial events.
// Handlers are NOT wired up yet — event types are defined here for future use.

/// <summary>
/// Raised when a new Order is created.
/// Platform/Compliance module can subscribe to trigger screening or audit logging.
/// </summary>
public record OrderCreatedEvent(
    Guid TenantId,
    Guid OrderId,
    string OrderType,
    Guid? PayerPartyId,
    decimal AmountIn,
    string CurrencyIn) : IIntegrationEvent;

/// <summary>
/// Raised when an Order's status changes (e.g. Submitted, Approved, Completed, Cancelled).
/// Other modules can react to order lifecycle transitions.
/// </summary>
public record OrderStatusChangedEvent(
    Guid TenantId,
    Guid OrderId,
    string PreviousStatus,
    string NewStatus) : IIntegrationEvent;

/// <summary>
/// Raised when a Payment is completed successfully.
/// Used for ledger posting, notifications, and compliance reconciliation.
/// </summary>
public record PaymentCompletedEvent(
    Guid TenantId,
    Guid PaymentId,
    Guid? OrderId,
    decimal Amount,
    string Currency) : IIntegrationEvent;

/// <summary>
/// Raised when an Invoice is issued to a customer.
/// Platform/Notifications module can subscribe to send invoice emails.
/// </summary>
public record InvoiceIssuedEvent(
    Guid TenantId,
    Guid InvoiceId,
    Guid CustomerAccountId,
    decimal TotalAmount,
    string Currency) : IIntegrationEvent;

/// <summary>
/// Raised when an Invoice is fully paid.
/// Triggers downstream workflows (e.g. order fulfilment, receipt generation).
/// </summary>
public record InvoicePaidEvent(
    Guid TenantId,
    Guid InvoiceId,
    Guid CustomerAccountId) : IIntegrationEvent;

/// <summary>
/// Raised when a ledger journal entry is posted.
/// AI/Insight module can subscribe for real-time analytics.
/// </summary>
public record JournalEntryPostedEvent(
    Guid TenantId,
    Guid JournalEntryId,
    Guid LedgerId,
    decimal Amount,
    string Currency) : IIntegrationEvent;
