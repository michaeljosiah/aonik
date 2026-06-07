using System.Diagnostics;

namespace Aonik.Finance.Services.Observability;

/// <summary>
/// Distributed-trace source for money-touching paths in the Finance module.
/// Every quote, confirm, capture, transmit, settle, and webhook stage MUST
/// open an activity from this source so all spans correlate by OrderId in
/// App Insights (GitHub Issue #142 — operators must be able to retrieve the
/// full trace for a given OrderId in under 30 seconds).
/// </summary>
/// <remarks>
/// Source name "Aonik.Finance" is registered in
/// <c>Aonik.ServiceDefaults.Extensions.ConfigureOpenTelemetry.WithTracing</c>.
/// Set the <see cref="OrderIdTag"/> on every span before any computation;
/// downstream KQL queries pivot on it. Pair with <c>MoneyActionLog</c>
/// for structured-log entries (the two sources are unioned in the
/// money-action-by-orderid.kql query).
/// </remarks>
public static class FinanceActivitySource
{
    public const string Name = "Aonik.Finance";
    public const string Version = "1.0.0";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> for money-action spans.
    /// Lifetime is the process; intentionally not disposed (idiomatic for
    /// process-singleton sources).
    /// </summary>
    public static readonly ActivitySource Source = new(Name, Version);

    /// <summary>
    /// Standard tag key for the OrderId attached to every money-action span.
    /// </summary>
    /// <remarks>
    /// PascalCase by design: these are domain attributes (not OTel semantic
    /// conventions like <c>http.method</c> / <c>db.statement</c>), and they
    /// MUST match the customDimensions key emitted by
    /// <c>MoneyActionLog</c> / <c>BeginOrderScope</c> so the saved KQL
    /// query can filter and project on one key per concept without
    /// coalescing dot.case and PascalCase variants. The KQL-side
    /// regression test in <c>MoneyActionLogTests</c> locks this in.
    /// </remarks>
    public const string OrderIdTag = "OrderId";

    /// <summary>Standard tag key for the TenantId. PascalCase to match ILogger.</summary>
    public const string TenantIdTag = "TenantId";

    /// <summary>
    /// Standard tag key for the money-action lifecycle stage. Values are
    /// drawn from <see cref="MoneyActionStages"/> (quote, confirm, capture,
    /// transmit, settle, webhook). PascalCase to match the
    /// <c>Stage</c> structured-log scope key.
    /// </summary>
    public const string StageTag = "Stage";

    /// <summary>
    /// Standard tag key for the money-action outcome (success, failed,
    /// skipped_idempotent, rejected, timeout). PascalCase to match the
    /// <c>Outcome</c> structured-log scope key.
    /// </summary>
    public const string OutcomeTag = "Outcome";

    /// <summary>
    /// Standard tag key for the PaymentIntent identifier on capture /
    /// transmit / settle spans. PascalCase to match ILogger.
    /// </summary>
    public const string PaymentIntentIdTag = "PaymentIntentId";

    /// <summary>
    /// Standard tag key for the Invoice identifier on settle spans where
    /// the path is invoice-driven. PascalCase to match ILogger.
    /// </summary>
    public const string InvoiceIdTag = "InvoiceId";

    /// <summary>
    /// Standard tag key for the PricingQuoteId attached to Quote-stage spans
    /// and to the Confirm-stage span for chaining. PascalCase to match
    /// the <c>PricingQuoteId</c> structured-log property.
    /// </summary>
    public const string PricingQuoteIdTag = "PricingQuoteId";

    /// <summary>
    /// Standard tag key for the JournalEntryId on settle spans. PascalCase
    /// to match the <c>JournalEntryId</c> structured-log property emitted
    /// by <c>MoneyActionLog.LedgerPosted</c>.
    /// </summary>
    public const string JournalEntryIdTag = "JournalEntryId";
}

/// <summary>
/// Canonical lifecycle-stage values for the <c>Stage</c> span tag and the
/// <c>Stage</c> structured-log field. Treat as a closed set; do not add
/// new values without updating the saved KQL query and the operator runbook.
/// </summary>
public static class MoneyActionStages
{
    public const string Quote = "quote";
    public const string Confirm = "confirm";
    public const string Capture = "capture";
    public const string Transmit = "transmit";
    public const string Settle = "settle";
    public const string Webhook = "webhook";
}

/// <summary>
/// Canonical outcome values for the <c>Outcome</c> span tag and the
/// <c>Outcome</c> structured-log field. Treat as a closed set.
/// </summary>
public static class MoneyActionOutcomes
{
    public const string Success = "success";
    public const string Failed = "failed";
    public const string Rejected = "rejected";
    public const string SkippedIdempotent = "skipped_idempotent";
    public const string Timeout = "timeout";
}
