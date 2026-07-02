using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class ListLowStockAlertsEndpoint : EndpointWithoutRequest<IReadOnlyList<LowStockAlertDto>>
{
    private readonly ILowStockAlertService _alerts;

    public ListLowStockAlertsEndpoint(ILowStockAlertService alerts) => _alerts = alerts;

    public override void Configure()
    {
        Get("/commerce/admin/low-stock-alerts");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List the tenant's low-stock alerts, newest first (optionally filtered by status: Open, Acknowledged, Ordered, Resolved).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = Query<string?>("status", isRequired: false);
        var result = await _alerts.ListAsync(status, ct);
        await Send.OkAsync(result, ct);
    }
}
