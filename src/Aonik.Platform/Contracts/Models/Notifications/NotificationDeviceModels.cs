namespace Aonik.Platform.Contracts.Models.Notifications;

public record RegisterNotificationDeviceRequest(
    string Provider,
    string Platform,
    string DeviceToken);

public record NotificationDeviceResponse(
    Guid NotificationDeviceId,
    string Provider,
    string Platform,
    DateTime LastSeenAtUtc);

public record PushNotificationTarget(
    Guid NotificationDeviceId,
    string Provider,
    string Platform,
    string DeviceToken);

public record PushNotificationDispatchRequest(
    Guid TenantId,
    Guid UserId,
    string Type,
    string Source,
    string Title,
    string Body,
    string Severity,
    string? ActionUrl,
    IReadOnlyCollection<PushNotificationTarget> Targets);

public record PushNotificationDispatchResult(
    IReadOnlyCollection<Guid> InvalidDeviceIds);
