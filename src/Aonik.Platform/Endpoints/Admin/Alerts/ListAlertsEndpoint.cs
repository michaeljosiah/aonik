using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Alerts;

internal sealed record ListAlertsRequest(int Take = 50);

internal sealed class ListAlertsEndpoint : Endpoint<ListAlertsRequest, AlertListResponse>
{
    private readonly IAlertAdminService _alertAdminService;

    public ListAlertsEndpoint(IAlertAdminService alertAdminService)
    {
        _alertAdminService = alertAdminService;
    }

    public override void Configure()
    {
        Get("/admin/alerts");
        Policies("PlatformAdmin");
    }

    public override async Task HandleAsync(ListAlertsRequest req, CancellationToken ct)
    {
        var result = await _alertAdminService.ListAlertsAsync(req.Take, ct);
        await Send.OkAsync(result, ct);
    }
}
