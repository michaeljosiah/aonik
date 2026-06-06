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

    /// <summary>
    /// Creates an in-app notification for one user. When <see cref="CreateNotificationRequest.IdempotencyKey"/>
    /// is set, creation is idempotent: a second call with the same (tenant, user, key) returns the existing
    /// notification instead of inserting a duplicate, enforced atomically by a unique index — safe even when
    /// two workers race (e.g. a reclaimed task dispatch re-running the same occurrence).
    /// </summary>
    Task<NotificationResponse> CreateForUserAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationBulkActionResponse> CreateForUsersAsync(
        CreateNotificationsRequest request,
        CancellationToken cancellationToken = default);
}
