using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class ListNotificationTemplatesEndpoint : EndpointWithoutRequest<List<NotificationTemplateSummary>>
{
    private readonly INotificationTemplateService _service;

    public ListNotificationTemplatesEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/notification-templates");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var channel = Query<string>("channel", false);
        var isActive = Query<bool?>("isActive", false);

        var results = await _service.ListTemplatesAsync(channel, isActive, ct);
        await Send.OkAsync(results, ct);
    }
}
