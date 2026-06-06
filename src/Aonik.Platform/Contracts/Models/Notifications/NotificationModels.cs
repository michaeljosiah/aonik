namespace Aonik.Platform.Contracts.Models.Notifications;

public record NotificationListRequest(
    string? Status,
    int Take = 50,
    DateTime? Before = null,
    bool IncludeDismissed = false);

public record NotificationResponse(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Channel,
    string Type,
    string Source,
    string Title,
    string Body,
    string Severity,
    string Status,
    string? ActionUrl,
    string? CorrelationId,
    Guid? AiRunId,
    string MetadataJson,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? DismissedAt);

public record NotificationSummaryResponse(
    int UnreadCount);

public record NotificationBulkActionResponse(
    int AffectedCount);

public record CreateNotificationRequest(
    Guid TenantId,
    Guid UserId,
    string Type,
    string Source,
    string Title,
    string Body,
    string Severity,
    string? ActionUrl,
    string? CorrelationId,
    Guid? AiRunId,
    string? MetadataJson = null,
    string Channel = Aonik.Platform.Notifications.NotificationChannels.InApp,
    // Optional dedupe key. When set, creation is idempotent — at most one notification per
    // (tenant, user, key), enforced by a unique index. Leave null when duplicates are acceptable.
    string? IdempotencyKey = null);

public record CreateNotificationsRequest(
    Guid TenantId,
    IReadOnlyCollection<Guid> UserIds,
    string Type,
    string Source,
    string Title,
    string Body,
    string Severity,
    string? ActionUrl,
    string? CorrelationId,
    Guid? AiRunId,
    string? MetadataJson = null,
    string Channel = Aonik.Platform.Notifications.NotificationChannels.InApp);
