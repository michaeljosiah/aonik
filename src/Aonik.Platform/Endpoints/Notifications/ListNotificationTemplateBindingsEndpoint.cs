using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List notification template bindings";
            s.Description = "Returns all bindings that map notification events to their associated templates.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var results = await _service.ListBindingsAsync(ct);
        await Send.OkAsync(results, ct);
    }
}
