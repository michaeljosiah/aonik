using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Notifications;

internal sealed class MarkAllNotificationsReadEndpoint : EndpointWithoutRequest<NotificationBulkActionResponse>
{
    private readonly INotificationService _notificationService;

    public MarkAllNotificationsReadEndpoint(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override void Configure()
    {
        Post("/admin/notifications/read-all");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Mark all notifications as read";
            s.Description = "Marks all unread notifications for the current user as read in a single bulk operation.";
            s.Response(200, "All notifications marked read");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllReadAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
