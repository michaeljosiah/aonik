using Aonik.Platform.Contracts.Models.Notifications;

namespace Aonik.Platform.Contracts.Services.Notifications;

public interface INotificationService
{
    Task<List<NotificationResponse>> ListForCurrentUserAsync(
        NotificationListRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationSummaryResponse> GetSummaryForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> MarkReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> DismissAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<NotificationBulkActionResponse> MarkAllReadAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> CreateForUserAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationBulkActionResponse> CreateForUsersAsync(
        CreateNotificationsRequest request,
        CancellationToken cancellationToken = default);
}
