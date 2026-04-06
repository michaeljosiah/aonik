using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Notifications;

internal sealed class MarkNotificationReadEndpoint : EndpointWithoutRequest<NotificationResponse>
{
    private readonly INotificationService _notificationService;

    public MarkNotificationReadEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Post("/admin/notifications/{id}/read");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Mark notification as read";
            s.Description = "Marks the specified notification as read, decrementing the unread count.";
            s.Response(200, "Notification marked read");
            s.Response(401, "Not authenticated");
            s.Response(404, "Notification not found");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var notificationId = Route<Guid>("id");
        var result = await _notificationService.MarkReadAsync(notificationId, ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
