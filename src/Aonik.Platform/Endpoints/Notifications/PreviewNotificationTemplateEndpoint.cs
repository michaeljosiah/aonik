using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class PreviewNotificationTemplateEndpoint : Endpoint<PreviewNotificationTemplateRequest, PreviewNotificationTemplateResponse>
{
    private readonly INotificationTemplateService _service;

    public PreviewNotificationTemplateEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/notification-templates/preview");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(PreviewNotificationTemplateRequest req, CancellationToken ct)
    {
        var result = await _service.PreviewTemplateAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
