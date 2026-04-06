using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update a template binding";
            s.Description = "Updates an existing notification template binding's event or template association.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Binding not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(UpdateNotificationTemplateBindingRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.UpdateBindingAsync(id, req, ct);
        await Send.OkAsync(result, ct);
    }
}
