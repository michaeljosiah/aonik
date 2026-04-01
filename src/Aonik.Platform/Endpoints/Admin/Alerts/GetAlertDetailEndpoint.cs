using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Alerts;

internal sealed class GetAlertDetailEndpoint : EndpointWithoutRequest<AlertDetailResponse>
{
    private readonly IAlertAdminService _alertAdminService;

    public GetAlertDetailEndpoint(IAlertAdminService alertAdminService)
    {
        _alertAdminService = alertAdminService;
    }

    public override void Configure()
    {
        Get("/admin/alerts/{id}");
        Policies("PlatformAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _alertAdminService.GetAlertAsync(id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
