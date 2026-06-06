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

    /// <summary>
    /// True if a notification already exists for the given user carrying <paramref name="correlationId"/>.
    /// Lets a producer make creation idempotent (GET-before-act) — e.g. the task scheduler keys each
    /// occurrence's reminder on its run id so a retried or concurrently-reclaimed dispatch never posts
    /// the same notification twice.
    /// </summary>
    Task<bool> ExistsForUserByCorrelationAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<NotificationBulkActionResponse> CreateForUsersAsync(
        CreateNotificationsRequest request,
        CancellationToken cancellationToken = default);
}
