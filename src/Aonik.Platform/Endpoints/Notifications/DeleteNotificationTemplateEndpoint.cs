using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Notifications;

internal class DeleteNotificationTemplateEndpoint : EndpointWithoutRequest
{
    private readonly INotificationTemplateService _service;

    public DeleteNotificationTemplateEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/admin/notification-templates/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a notification template";
            s.Description = "Permanently removes a notification template by its unique identifier.";
            s.Response(204, "Template deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Template not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await _service.DeleteTemplateAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
