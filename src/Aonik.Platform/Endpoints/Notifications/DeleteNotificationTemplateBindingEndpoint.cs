using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await _service.DeleteBindingAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
