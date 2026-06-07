using Microsoft.Extensions.Logging;

// SYSLIB1015: `tenantId` is intentionally not in the message template — it is
//             emitted as a structured property anyway and consumed by App
//             Insights customDimensions filtering. Keeping it out of the
//             human-readable message keeps the line readable while still
//             making it queryable.
// SYSLIB1025: Every method here shares EventName = "MoneyAction" by design,
//             so the saved KQL query can filter on
//             `customDimensions.EventName == "MoneyAction"` and return every
//             lifecycle milestone with a single predicate.
#pragma warning disable SYSLIB1015, SYSLIB1025

namespace Aonik.Finance.Services.Observability;

/// <summary>
/// Source-generated structured logger for money-touching code paths
/// (GitHub Issue #142). Every public method is an extension on
/// <see cref="ILogger"/>; the public wrapper opens a <c>BeginScope</c>
/// adding <c>Stage</c> and <c>Outcome</c> as structured properties so
/// saved KQL queries can pivot on them uniformly, then delegates to a
/// private source-generated method that emits the typed event.
/// </summary>
/// <remarks>
/// EventId schema:
/// <list type="bullet">
///   <item>11xx — Quote stage</item>
///   <item>12xx — Confirm stage</item>
///   <item>13xx — Capture stage</item>
///   <item>14xx — Transmit stage</item>
///   <item>15xx — Settle stage</item>
///   <item>16xx — Webhook stage</item>
/// </list>
/// All events share <c>EventName = "MoneyAction"</c> so one App Insights
/// query — <c>traces | where customDimensions.EventName == "MoneyAction"</c>
/// — returns every milestone across the lifecycle. Pair with
/// <see cref="FinanceActivitySource"/> spans for distributed tracing;
/// the two are unioned in <c>money-action-by-orderid.kql</c>.
/// </remarks>
public static partial class MoneyActionLog
{
    private const string EventName = "MoneyAction";

    private static IDisposable? BeginStageScope(ILogger logger, string stage, string outcome) =>
        logger.BeginScope(new Dictionary<string, object>
        {
            ["Stage"] = stage,
            ["Outcome"] = outcome,
        });

    // --- Quote stage (11xx) ---------------------------------------------
    // No OrderId at quote time — the quote precedes the order. Correlation
    // key is PricingQuoteId; the Confirm-stage log carries both ids so the
    // saved KQL query can chain from OrderId → PricingQuoteId → quote logs.

    public static void QuoteCreated(this ILogger logger, Guid pricingQuoteId, Guid tenantId, string action, decimal amount, string currency)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Quote, MoneyActionOutcomes.Success);
        QuoteCreatedCore(logger, pricingQuoteId, tenantId, action, amount, currency);
    }

    public static void QuoteFailed(this ILogger logger, Guid? pricingQuoteId, Guid tenantId, string action, string reason, Exception? exception = null)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Quote, MoneyActionOutcomes.Failed);
        QuoteFailedCore(logger, pricingQuoteId ?? Guid.Empty, tenantId, action, reason, exception);
    }

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, EventName = EventName,
        Message = "Quote created (id {PricingQuoteId}) — {Action}: {Currency} {Amount}")]
    private static partial void QuoteCreatedCore(ILogger logger, Guid pricingQuoteId, Guid tenantId, string action, decimal amount, string currency);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, EventName = EventName,
        Message = "Quote failed (id {PricingQuoteId}) — {Action}: {Reason}")]
    private static partial void QuoteFailedCore(ILogger logger, Guid pricingQuoteId, Guid tenantId, string action, string reason, Exception? exception);

    // --- Confirm stage (12xx) -------------------------------------------
    // Confirm carries BOTH OrderId AND PricingQuoteId — this is the join
    // point that lets KQL trace back from an OrderId to its quote logs.

    public static void OrderConfirmed(this ILogger logger, Guid orderId, Guid tenantId, string action, Guid? pricingQuoteId = null)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Confirm, MoneyActionOutcomes.Success);
        OrderConfirmedCore(logger, orderId, tenantId, action, pricingQuoteId ?? Guid.Empty);
    }

    public static void OrderRejected(this ILogger logger, Guid orderId, Guid tenantId, string reason)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Confirm, MoneyActionOutcomes.Rejected);
        OrderRejectedCore(logger, orderId, tenantId, reason);
    }

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, EventName = EventName,
        Message = "Order {OrderId} confirmed ({Action}) — quote {PricingQuoteId}")]
    private static partial void OrderConfirmedCore(ILogger logger, Guid orderId, Guid tenantId, string action, Guid pricingQuoteId);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning, EventName = EventName,
        Message = "Order {OrderId} rejected: {Reason}")]
    private static partial void OrderRejectedCore(ILogger logger, Guid orderId, Guid tenantId, string reason);

    // --- Capture stage (13xx) -------------------------------------------

    public static void PaymentCaptured(this ILogger logger, Guid orderId, Guid tenantId, Guid paymentIntentId, decimal amount, string currency)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Capture, MoneyActionOutcomes.Success);
        PaymentCapturedCore(logger, orderId, tenantId, paymentIntentId, amount, currency);
    }

    public static void PaymentCaptureFailed(this ILogger logger, Guid orderId, Guid tenantId, Guid paymentIntentId, string reason, Exception? exception = null)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Capture, MoneyActionOutcomes.Failed);
        PaymentCaptureFailedCore(logger, orderId, tenantId, paymentIntentId, reason, exception);
    }

    public static void PaymentCaptureSkippedIdempotent(this ILogger logger, Guid orderId, Guid tenantId, Guid paymentIntentId)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Capture, MoneyActionOutcomes.SkippedIdempotent);
        PaymentCaptureSkippedIdempotentCore(logger, orderId, tenantId, paymentIntentId);
    }

    [LoggerMessage(EventId = 1301, Level = LogLevel.Information, EventName = EventName,
        Message = "Payment captured for order {OrderId} (intent {PaymentIntentId}) — {Currency} {Amount}")]
    private static partial void PaymentCapturedCore(ILogger logger, Guid orderId, Guid tenantId, Guid paymentIntentId, decimal amount, string currency);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Error, EventName = EventName,
        Message = "Payment capture failed for order {OrderId} (intent {PaymentIntentId}): {Reason}")]
    private static partial void PaymentCaptureFailedCore(ILogger logger, Guid orderId, Guid tenantId, Guid paymentIntentId, string reason, Exception? exception);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Information, EventName = EventName,
        Message = "Payment capture skipped (idempotent) for order {OrderId} (intent {PaymentIntentId})")]
    private static partial void PaymentCaptureSkippedIdempotentCore(ILogger logger, Guid orderId, Guid tenantId, Guid paymentIntentId);

    // --- Transmit stage (14xx) ------------------------------------------

    public static void PaymentTransmitted(this ILogger logger, Guid orderId, Guid tenantId, string partner, string externalReference)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Transmit, MoneyActionOutcomes.Success);
        PaymentTransmittedCore(logger, orderId, tenantId, partner, externalReference);
    }

    public static void PaymentTransmitFailed(this ILogger logger, Guid orderId, Guid tenantId, string partner, string reason, Exception? exception = null)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Transmit, MoneyActionOutcomes.Failed);
        PaymentTransmitFailedCore(logger, orderId, tenantId, partner, reason, exception);
    }

    public static void PaymentTransmitTimeout(this ILogger logger, Guid orderId, Guid tenantId, string partner, long elapsedMs)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Transmit, MoneyActionOutcomes.Timeout);
        PaymentTransmitTimeoutCore(logger, orderId, tenantId, partner, elapsedMs);
    }

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information, EventName = EventName,
        Message = "Payment transmitted for order {OrderId} via {Partner} — {ExternalReference}")]
    private static partial void PaymentTransmittedCore(ILogger logger, Guid orderId, Guid tenantId, string partner, string externalReference);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Error, EventName = EventName,
        Message = "Payment transmit failed for order {OrderId} via {Partner}: {Reason}")]
    private static partial void PaymentTransmitFailedCore(ILogger logger, Guid orderId, Guid tenantId, string partner, string reason, Exception? exception);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Warning, EventName = EventName,
        Message = "Payment transmit timed out for order {OrderId} via {Partner} after {ElapsedMs}ms")]
    private static partial void PaymentTransmitTimeoutCore(ILogger logger, Guid orderId, Guid tenantId, string partner, long elapsedMs);

    // --- Settle stage (15xx) --------------------------------------------
    // OrderId is nullable here: invoice-driven settlement (BillingService
    // .MarkInvoiceAsPaid → PostInvoiceSettlement) need not carry an Order,
    // while payment-capture-driven settlement (PaymentService.Capture →
    // PostPaymentCapture) always does.

    public static void LedgerPosted(this ILogger logger, Guid? orderId, Guid tenantId, Guid journalEntryId, decimal amount, string currency)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Settle, MoneyActionOutcomes.Success);
        LedgerPostedCore(logger, orderId ?? Guid.Empty, tenantId, journalEntryId, amount, currency);
    }

    public static void LedgerPostSkippedIdempotent(this ILogger logger, Guid? orderId, Guid tenantId)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Settle, MoneyActionOutcomes.SkippedIdempotent);
        LedgerPostSkippedIdempotentCore(logger, orderId ?? Guid.Empty, tenantId);
    }

    public static void LedgerPostFailed(this ILogger logger, Guid? orderId, Guid tenantId, string reason, Exception? exception = null)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Settle, MoneyActionOutcomes.Failed);
        LedgerPostFailedCore(logger, orderId ?? Guid.Empty, tenantId, reason, exception);
    }

    [LoggerMessage(EventId = 1501, Level = LogLevel.Information, EventName = EventName,
        Message = "Ledger posted for order {OrderId} — journal {JournalEntryId} ({Currency} {Amount})")]
    private static partial void LedgerPostedCore(ILogger logger, Guid orderId, Guid tenantId, Guid journalEntryId, decimal amount, string currency);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Information, EventName = EventName,
        Message = "Ledger post skipped (idempotent) for order {OrderId} — already posted")]
    private static partial void LedgerPostSkippedIdempotentCore(ILogger logger, Guid orderId, Guid tenantId);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Error, EventName = EventName,
        Message = "Ledger post failed for order {OrderId}: {Reason}")]
    private static partial void LedgerPostFailedCore(ILogger logger, Guid orderId, Guid tenantId, string reason, Exception? exception);

    // --- Webhook stage (16xx) -------------------------------------------

    public static void WebhookReceived(this ILogger logger, Guid orderId, Guid tenantId, string source, string eventKind)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Webhook, MoneyActionOutcomes.Success);
        WebhookReceivedCore(logger, orderId, tenantId, source, eventKind);
    }

    public static void WebhookProcessed(this ILogger logger, Guid orderId, Guid tenantId, string source, string eventKind, string outcome)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Webhook, outcome);
        WebhookProcessedCore(logger, orderId, tenantId, source, eventKind, outcome);
    }

    public static void WebhookRejected(this ILogger logger, Guid orderId, Guid tenantId, string source, string reason)
    {
        using var _ = BeginStageScope(logger, MoneyActionStages.Webhook, MoneyActionOutcomes.Rejected);
        WebhookRejectedCore(logger, orderId, tenantId, source, reason);
    }

    [LoggerMessage(EventId = 1601, Level = LogLevel.Information, EventName = EventName,
        Message = "Webhook received for order {OrderId} from {Source} — {EventKind}")]
    private static partial void WebhookReceivedCore(ILogger logger, Guid orderId, Guid tenantId, string source, string eventKind);

    [LoggerMessage(EventId = 1602, Level = LogLevel.Information, EventName = EventName,
        Message = "Webhook processed for order {OrderId} from {Source} — {EventKind}: {Outcome}")]
    private static partial void WebhookProcessedCore(ILogger logger, Guid orderId, Guid tenantId, string source, string eventKind, string outcome);

    [LoggerMessage(EventId = 1603, Level = LogLevel.Warning, EventName = EventName,
        Message = "Webhook rejected for order {OrderId} from {Source}: {Reason}")]
    private static partial void WebhookRejectedCore(ILogger logger, Guid orderId, Guid tenantId, string source, string reason);
}
