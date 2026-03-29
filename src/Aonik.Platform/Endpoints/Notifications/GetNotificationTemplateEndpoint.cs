using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class GetNotificationTemplateEndpoint : EndpointWithoutRequest<NotificationTemplateResponse>
{
    private readonly INotificationTemplateService _service;

    public GetNotificationTemplateEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/notification-templates/{id}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.GetTemplateAsync(id, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
