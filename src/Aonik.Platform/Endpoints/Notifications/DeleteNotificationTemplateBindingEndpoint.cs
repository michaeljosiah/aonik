using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Notifications;

internal class DeleteNotificationTemplateBindingEndpoint : EndpointWithoutRequest
{
    private readonly INotificationTemplateService _service;

    public DeleteNotificationTemplateBindingEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/admin/notification-template-bindings/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a template binding";
            s.Description = "Permanently removes a notification template binding by its unique identifier.";
            s.Response(204, "Binding deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Binding not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await _service.DeleteBindingAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
