using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update a notification template";
            s.Description = "Updates an existing notification template's channel, subject, body, or active status.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Template not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(UpdateNotificationTemplateRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.UpdateTemplateAsync(id, req, ct);
        await Send.OkAsync(result, ct);
    }
}
