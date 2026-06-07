using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Observability;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class GetMoneyActionTraceRequest
{
    /// <summary>
    /// The OrderId an operator wants to retrieve the full money-action
    /// lifecycle trace for. Resolved from the route segment.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// Lookback window. Same closed set as the other observability
    /// endpoints — "1h", "24h" (default), "7d", "30d".
    /// </summary>
    [QueryParam]
    public string TimeRange { get; init; } = "24h";
}

/// <summary>
/// Returns every log entry, custom event, dependency call, and exception
/// tied to a given OrderId across the money-action lifecycle (Quote /
/// Confirm / Capture / Transmit / Settle / Webhook) — GitHub Issue #142.
/// Backed by <c>AppInsightsQueryService.GetMoneyActionTraceAsync</c>,
/// which runs the same KQL as
/// <c>docs/observability/queries/money-action-by-orderid.kql</c>. The
/// response includes the query wall-clock so operators can watch the
/// 30 s SLA in the Admin UI.
/// </summary>
internal class GetMoneyActionTraceEndpoint
    : Endpoint<GetMoneyActionTraceRequest, MoneyActionTraceResponse>
{
    private readonly IObservabilityService _observabilityService;

    public GetMoneyActionTraceEndpoint(IObservabilityService observabilityService)
    {
        _observabilityService = observabilityService;
    }

    public override void Configure()
    {
        Get("/admin/observability/money-actions/{OrderId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get money-action trace for an OrderId";
            s.Description =
                "Returns every observable signal tied to the given OrderId across the money-action lifecycle " +
                "(Quote / Confirm / Capture / Transmit / Settle / Webhook). The Quote stage is chained via " +
                "PricingQuoteId resolved from the Confirm-stage log. Response includes the query wall-clock " +
                "so the 30s SLA from Issue #142 can be watched in the UI.";
            s.Response(200, "Trace timeline + envelope (Configured/OrderId/PricingQuoteId/QueryDurationMs)");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(GetMoneyActionTraceRequest req, CancellationToken ct)
    {
        var result = await _observabilityService.GetMoneyActionTraceAsync(req.OrderId, req.TimeRange, ct);
        await Send.OkAsync(result, ct);
    }
}
