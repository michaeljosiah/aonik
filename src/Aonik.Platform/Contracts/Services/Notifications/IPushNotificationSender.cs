using Aonik.Platform.Contracts.Models.Notifications;

namespace Aonik.Platform.Contracts.Services.Notifications;

public interface IPushNotificationSender
{
    Task<PushNotificationDispatchResult> SendAsync(
        PushNotificationDispatchRequest request,
        CancellationToken cancellationToken = default);
}
