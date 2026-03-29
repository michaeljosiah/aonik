using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class UpdateNotificationTemplateEndpoint : Endpoint<UpdateNotificationTemplateRequest, NotificationTemplateResponse>
{
    private readonly INotificationTemplateService _service;

    public UpdateNotificationTemplateEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/admin/notification-templates/{id}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(UpdateNotificationTemplateRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.UpdateTemplateAsync(id, req, ct);
        await Send.OkAsync(result, ct);
    }
}
