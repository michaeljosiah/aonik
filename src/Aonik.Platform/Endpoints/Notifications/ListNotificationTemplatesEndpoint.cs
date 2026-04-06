using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List notification templates";
            s.Description = "Returns all notification templates, optionally filtered by channel and active status.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var channel = Query<string>("channel", false);
        var isActive = Query<bool?>("isActive", false);

        var results = await _service.ListTemplatesAsync(channel, isActive, ct);
        await Send.OkAsync(results, ct);
    }
}
