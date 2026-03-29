using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await _service.DeleteTemplateAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
