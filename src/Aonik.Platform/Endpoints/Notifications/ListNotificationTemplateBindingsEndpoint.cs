using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class ListNotificationTemplateBindingsEndpoint : EndpointWithoutRequest<List<NotificationTemplateBindingResponse>>
{
    private readonly INotificationTemplateService _service;

    public ListNotificationTemplateBindingsEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/notification-template-bindings");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var results = await _service.ListBindingsAsync(ct);
        await Send.OkAsync(results, ct);
    }
}
