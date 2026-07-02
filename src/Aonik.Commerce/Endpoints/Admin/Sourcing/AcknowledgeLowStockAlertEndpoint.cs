using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class AcknowledgeLowStockAlertEndpoint : EndpointWithoutRequest<LowStockAlertDto>
{
    private readonly ILowStockAlertService _alerts;

    public AcknowledgeLowStockAlertEndpoint(ILowStockAlertService alerts) => _alerts = alerts;

    public override void Configure()
    {
        Post("/commerce/admin/low-stock-alerts/{alertId:guid}/acknowledge");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Acknowledge an open low-stock alert (an operator is handling it). The alert stays active — the scan refreshes it rather than raising a second.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var alertId = Route<Guid>("alertId");
        var result = await _alerts.AcknowledgeAsync(alertId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
