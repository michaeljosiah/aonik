using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _notificationService.MarkAllReadAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
