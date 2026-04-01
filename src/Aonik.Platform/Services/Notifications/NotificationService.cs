using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Notifications;

internal sealed class NotificationService : INotificationService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly IClock _clock;

    public NotificationService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        INotificationRealtimePublisher realtimePublisher,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _realtimePublisher = realtimePublisher;
        _clock = clock;
    }

    public async Task<List<NotificationResponse>> ListForCurrentUserAsync(
        NotificationListRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetCurrentContext();
        var take = Math.Clamp(request.Take, 1, 100);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId);

        if (!request.IncludeDismissed && string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.Status != NotificationStatuses.Dismissed);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.Status == request.Status.Trim());
        }

        if (request.Before.HasValue)
        {
            query = query.Where(x => x.CreatedAt < request.Before.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => MapResponse(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationSummaryResponse> GetSummaryForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetCurrentContext();

        var unreadCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId
                    && x.UserId == userId
                    && x.Status == NotificationStatuses.Unread,
                cancellationToken);

        return new NotificationSummaryResponse(unreadCount);
    }

    public async Task<NotificationResponse?> MarkReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetCurrentContext();
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && x.TenantId == tenantId && x.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return null;
        }

        if (notification.Status != NotificationStatuses.Unread)
        {
            return MapResponse(notification);
        }

        notification.Status = NotificationStatuses.Read;
        notification.ReadAt ??= _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = MapResponse(notification);
        await _realtimePublisher.PublishAsync(new NotificationRealtimeEvent("NOTIFICATION_UPDATED", response, -1), cancellationToken);
        return response;
    }

    public async Task<NotificationResponse?> DismissAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetCurrentContext();
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && x.TenantId == tenantId && x.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return null;
        }

        if (notification.Status == NotificationStatuses.Dismissed)
        {
            return MapResponse(notification);
        }

        notification.Status = NotificationStatuses.Dismissed;
        notification.DismissedAt ??= _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = MapResponse(notification);
        var unreadCountDelta = notification.ReadAt.HasValue ? 0 : -1;
        await _realtimePublisher.PublishAsync(new NotificationRealtimeEvent("NOTIFICATION_UPDATED", response, unreadCountDelta), cancellationToken);
        return response;
    }

    public async Task<NotificationBulkActionResponse> MarkAllReadAsync(
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetCurrentContext();
        var utcNow = _clock.UtcNow;

        var notifications = await _dbContext.Notifications
            .Where(
                x => x.TenantId == tenantId
                    && x.UserId == userId
                    && x.Status == NotificationStatuses.Unread)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return new NotificationBulkActionResponse(0);
        }

        foreach (var notification in notifications)
        {
            notification.Status = NotificationStatuses.Read;
            notification.ReadAt ??= utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(
                new NotificationRealtimeEvent("NOTIFICATION_UPDATED", MapResponse(notification), -1),
                cancellationToken);
        }

        return new NotificationBulkActionResponse(notifications.Count);
    }

    public async Task<NotificationResponse> CreateForUserAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request.TenantId, request.UserId, request.Type, request.Source, request.Title, request.Body, request.Severity, request.Channel);

        var notification = new Notification
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            Channel = request.Channel.Trim(),
            Type = request.Type.Trim(),
            Source = request.Source.Trim(),
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Severity = request.Severity.Trim(),
            Status = NotificationStatuses.Unread,
            ActionUrl = TrimNullable(request.ActionUrl),
            CorrelationId = TrimNullable(request.CorrelationId),
            AiRunId = request.AiRunId,
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}" : request.MetadataJson.Trim(),
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = MapResponse(notification);
        await _realtimePublisher.PublishAsync(new NotificationRealtimeEvent("NOTIFICATION_CREATED", response, 1), cancellationToken);
        return response;
    }

    public async Task<NotificationBulkActionResponse> CreateForUsersAsync(
        CreateNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userIds = request.UserIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(request));
        }

        if (userIds.Count == 0)
        {
            return new NotificationBulkActionResponse(0);
        }

        ValidateSharedCreateRequest(request.Type, request.Source, request.Title, request.Body, request.Severity, request.Channel);

        var notifications = userIds
            .Select(userId => new Notification
            {
                TenantId = request.TenantId,
                UserId = userId,
                Channel = request.Channel.Trim(),
                Type = request.Type.Trim(),
                Source = request.Source.Trim(),
                Title = request.Title.Trim(),
                Body = request.Body.Trim(),
                Severity = request.Severity.Trim(),
                Status = NotificationStatuses.Unread,
                ActionUrl = TrimNullable(request.ActionUrl),
                CorrelationId = TrimNullable(request.CorrelationId),
                AiRunId = request.AiRunId,
                MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}" : request.MetadataJson.Trim(),
            })
            .ToList();

        _dbContext.Notifications.AddRange(notifications);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(
                new NotificationRealtimeEvent("NOTIFICATION_CREATED", MapResponse(notification), 1),
                cancellationToken);
        }

        return new NotificationBulkActionResponse(notifications.Count);
    }

    private (Guid TenantId, Guid UserId) GetCurrentContext()
    {
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            throw new InvalidOperationException("Tenant context missing.");
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Current user context missing.");
        }

        return (tenantId, userId);
    }

    private static NotificationResponse MapResponse(Notification notification)
        => new(
            notification.Id,
            notification.TenantId,
            notification.UserId,
            notification.Channel,
            notification.Type,
            notification.Source,
            notification.Title,
            notification.Body,
            notification.Severity,
            notification.Status,
            notification.ActionUrl,
            notification.CorrelationId,
            notification.AiRunId,
            notification.MetadataJson,
            notification.CreatedAt,
            notification.ReadAt,
            notification.DismissedAt);

    private static void ValidateCreateRequest(
        Guid tenantId,
        Guid userId,
        string type,
        string source,
        string title,
        string body,
        string severity,
        string channel)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        ValidateSharedCreateRequest(type, source, title, body, severity, channel);
    }

    private static void ValidateSharedCreateRequest(
        string type,
        string source,
        string title,
        string body,
        string severity,
        string channel)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Type is required.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Source is required.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required.", nameof(body));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Severity is required.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Channel is required.", nameof(channel));
        }
    }

    private static string? TrimNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
