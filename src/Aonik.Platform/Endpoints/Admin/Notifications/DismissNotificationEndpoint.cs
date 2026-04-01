using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;

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
