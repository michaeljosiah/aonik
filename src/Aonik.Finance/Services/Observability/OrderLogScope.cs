using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.Observability;

/// <summary>
/// Extension methods that open an <see cref="ILogger"/> scope carrying
/// the OrderId (and optionally PaymentIntent / Invoice ids) so every
/// log entry inside a money-path request inherits the correlation keys
/// (GitHub Issue #142). The broader scope is intentional — operators
/// triaging a failed order need to see EF queries, downstream service
/// calls, and exceptions associated with the same OrderId, not just
/// the typed <see cref="MoneyActionLog"/> milestones.
/// </summary>
/// <remarks>
/// <para>
/// Call site convention: in every money-touching endpoint handler,
/// resolve the OrderId from the route / payload / lookup as early as
/// possible, then:
/// </para>
/// <code>
/// using var _ = _logger.BeginOrderScope(orderId, paymentIntentId: intent.Id);
/// // ... rest of the handler
/// </code>
/// <para>
/// The scope keys (<c>OrderId</c>, <c>PaymentIntentId</c>, <c>InvoiceId</c>)
/// match the customDimensions read by the saved KQL query
/// <c>money-action-by-orderid.kql</c>; do not rename them without updating
/// the query and the operator runbook.
/// </para>
/// <para>
/// This composes with the request-level scope opened by
/// <c>LogScopeEnrichmentConfiguration</c> (TenantId / UserId /
/// RequestId / CorrelationId) — both scopes are active simultaneously,
/// so a log entry inside the order scope carries all of them.
/// </para>
/// </remarks>
public static class OrderLogScope
{
    /// <summary>
    /// Opens a logger scope carrying the OrderId. Returns a disposable;
    /// the scope ends when disposed (use <c>using var _ = ...</c>).
    /// </summary>
    public static IDisposable? BeginOrderScope(this ILogger logger, Guid orderId) =>
        logger.BeginScope(new Dictionary<string, object>
        {
            ["OrderId"] = orderId,
        });

    /// <summary>
    /// Opens a logger scope carrying the OrderId and optionally the
    /// PaymentIntent and Invoice ids that bind to it. Use this overload
    /// in capture / settle paths where the additional ids are already
    /// resolved — operators can then filter by PaymentIntentId or
    /// InvoiceId without re-joining tables.
    /// </summary>
    public static IDisposable? BeginOrderScope(this ILogger logger, Guid orderId, Guid? paymentIntentId = null, Guid? invoiceId = null)
    {
        var state = new Dictionary<string, object>(capacity: 3)
        {
            ["OrderId"] = orderId,
        };

        if (paymentIntentId.HasValue)
        {
            state["PaymentIntentId"] = paymentIntentId.Value;
        }

        if (invoiceId.HasValue)
        {
            state["InvoiceId"] = invoiceId.Value;
        }

        return logger.BeginScope(state);
    }
}
