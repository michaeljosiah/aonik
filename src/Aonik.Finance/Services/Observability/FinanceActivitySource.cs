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
    /// Matches the "OrderId" customDimensions key emitted by structured
    /// logs so the saved KQL union query can correlate both signal types.
    /// </summary>
    public const string OrderIdTag = "order.id";

    /// <summary>
    /// Standard tag key for the TenantId; lets multi-tenant dashboards
    /// break down money-action volume per tenant.
    /// </summary>
    public const string TenantIdTag = "tenant.id";

    /// <summary>
    /// Standard tag key for the money-action lifecycle stage. Values are
    /// drawn from <see cref="MoneyActionStages"/> (quote, confirm, capture,
    /// transmit, settle, webhook) so dashboards can group spans by stage.
    /// </summary>
    public const string StageTag = "money.stage";

    /// <summary>
    /// Standard tag key for the money-action outcome (success, failed,
    /// skipped_idempotent, rejected, timeout). Mirrors the Outcome field
    /// on <c>MoneyActionLog</c> entries.
    /// </summary>
    public const string OutcomeTag = "money.outcome";

    /// <summary>
    /// Standard tag key for the PaymentIntent identifier on capture /
    /// transmit / settle spans. Null on quote and confirm spans.
    /// </summary>
    public const string PaymentIntentIdTag = "payment_intent.id";

    /// <summary>
    /// Standard tag key for the Invoice identifier on settle spans where
    /// the path is invoice-driven (Billing.MarkInvoiceAsPaid).
    /// </summary>
    public const string InvoiceIdTag = "invoice.id";
}

/// <summary>
/// Canonical lifecycle-stage values for the <c>money.stage</c> span tag and
/// the <c>Stage</c> structured-log field. Treat as a closed set; do not add
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
/// Canonical outcome values for the <c>money.outcome</c> span tag and the
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
