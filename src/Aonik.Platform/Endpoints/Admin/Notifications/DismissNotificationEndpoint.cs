using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Notifications;

internal sealed class DismissNotificationEndpoint : EndpointWithoutRequest<NotificationResponse>
{
    private readonly INotificationService _notificationService;

    public DismissNotificationEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Post("/admin/notifications/{id}/dismiss");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Dismiss a notification";
            s.Description = "Marks the specified notification as dismissed so it no longer appears in the active list.";
            s.Response(200, "Notification dismissed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Notification not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var notificationId = Route<Guid>("id");
        var result = await _notificationService.DismissAsync(notificationId, ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
