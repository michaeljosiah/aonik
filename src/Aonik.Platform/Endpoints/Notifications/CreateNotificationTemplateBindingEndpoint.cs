using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a template binding";
            s.Description = "Creates a new binding that associates a notification event with a template.";
            s.Response(200, "Binding created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CreateNotificationTemplateBindingRequest req, CancellationToken ct)
    {
        var result = await _service.CreateBindingAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
