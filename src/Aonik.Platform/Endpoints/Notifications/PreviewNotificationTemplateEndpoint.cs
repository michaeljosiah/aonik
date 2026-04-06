using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Preview a notification template";
            s.Description = "Renders a notification template with sample data to preview the final output.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(PreviewNotificationTemplateRequest req, CancellationToken ct)
    {
        var result = await _service.PreviewTemplateAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
