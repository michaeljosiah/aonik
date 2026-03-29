using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class UpdateNotificationTemplateBindingEndpoint : Endpoint<UpdateNotificationTemplateBindingRequest, NotificationTemplateBindingResponse>
{
    private readonly INotificationTemplateService _service;

    public UpdateNotificationTemplateBindingEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/admin/notification-template-bindings/{id}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(UpdateNotificationTemplateBindingRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.UpdateBindingAsync(id, req, ct);
        await Send.OkAsync(result, ct);
    }
}
