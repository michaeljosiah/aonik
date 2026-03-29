using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class CreateNotificationTemplateBindingEndpoint : Endpoint<CreateNotificationTemplateBindingRequest, NotificationTemplateBindingResponse>
{
    private readonly INotificationTemplateService _service;

    public CreateNotificationTemplateBindingEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/notification-template-bindings");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateNotificationTemplateBindingRequest req, CancellationToken ct)
    {
        var result = await _service.CreateBindingAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
