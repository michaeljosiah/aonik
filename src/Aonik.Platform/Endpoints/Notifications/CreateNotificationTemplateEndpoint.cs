using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Notifications;

internal class CreateNotificationTemplateEndpoint : Endpoint<CreateNotificationTemplateRequest, NotificationTemplateResponse>
{
    private readonly INotificationTemplateService _service;

    public CreateNotificationTemplateEndpoint(INotificationTemplateService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/notification-templates");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateNotificationTemplateRequest req, CancellationToken ct)
    {
        var result = await _service.CreateTemplateAsync(req, ct);

        await Send.CreatedAtAsync<GetNotificationTemplateEndpoint>(
            routeValues: new { id = result.Id },
            responseBody: result,
            cancellation: ct);
    }
}
