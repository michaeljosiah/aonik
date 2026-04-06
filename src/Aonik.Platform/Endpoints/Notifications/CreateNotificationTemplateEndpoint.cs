using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a notification template";
            s.Description = "Creates a new notification template with the specified channel, subject, and body content.";
            s.Response(201, "Template created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
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
