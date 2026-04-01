using Aonik.Platform.Contracts.Models.Notifications;

namespace Aonik.Platform.Contracts.Services.Notifications;

public interface INotificationDeviceService
{
    Task<NotificationDeviceResponse> RegisterCurrentUserDeviceAsync(
        RegisterNotificationDeviceRequest request,
        CancellationToken cancellationToken = default);
}
