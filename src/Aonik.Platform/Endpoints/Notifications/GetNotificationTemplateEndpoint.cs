using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Notifications;

internal class GetNotificationTemplateEndpoint : EndpointWithoutRequest<NotificationTemplateResponse>
{
    private readonly INotificationTemplateService _service;

    public GetNotificationTemplateEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/notification-templates/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get notification template by ID";
            s.Description = "Retrieves a single notification template by its unique identifier.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Template not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _service.GetTemplateAsync(id, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
