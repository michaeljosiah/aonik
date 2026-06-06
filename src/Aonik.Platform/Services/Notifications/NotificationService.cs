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
    private readonly ITenantContext _tenantContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly NotificationRealtimePublisher _realtimePublisher;
    private readonly IPushNotificationSender _pushNotificationSender;
    private readonly IClock _clock;

    public NotificationService(
        PlatformDbContext dbContext,
        ITenantContext tenantContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        NotificationRealtimePublisher realtimePublisher,
        IPushNotificationSender pushNotificationSender,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _realtimePublisher = realtimePublisher;
        _pushNotificationSender = pushNotificationSender;
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
            .Where(x => x.UserId == userId && (x.TenantId == tenantId || x.TenantId == Guid.Empty));

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
                x => x.UserId == userId
                    && (x.TenantId == tenantId || x.TenantId == Guid.Empty)
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

        notification ??= await _dbContext.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && x.TenantId == Guid.Empty && x.UserId == userId,
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

        notification ??= await _dbContext.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && x.TenantId == Guid.Empty && x.UserId == userId,
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
                x => x.UserId == userId
                    && (x.TenantId == tenantId || x.TenantId == Guid.Empty)
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
        await SaveChangesForTenantAsync(request.TenantId, cancellationToken);

        var response = MapResponse(notification);
        await _realtimePublisher.PublishAsync(new NotificationRealtimeEvent("NOTIFICATION_CREATED", response, 1), cancellationToken);
        await SendPushNotificationsAsync([notification], cancellationToken);
        return response;
    }

    public async Task<bool> ExistsForUserByCorrelationAsync(
        Guid tenantId,
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId))
        {
            return false;
        }

        var key = correlationId.Trim();
        return await _dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId
                    && x.CorrelationId == key
                    && (x.TenantId == tenantId || x.TenantId == Guid.Empty),
                cancellationToken);
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
        await SaveChangesForTenantAsync(request.TenantId, cancellationToken);

        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(
                new NotificationRealtimeEvent("NOTIFICATION_CREATED", MapResponse(notification), 1),
                cancellationToken);
        }

        await SendPushNotificationsAsync(notifications, cancellationToken);

        return new NotificationBulkActionResponse(notifications.Count);
    }

    private async Task SendPushNotificationsAsync(
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        var userIds = notifications
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var devices = await _dbContext.NotificationDevices
            .Where(x => userIds.Contains(x.UserId) && x.IsActive)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return;
        }

        var devicesByUserId = devices
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<NotificationDevice>)x.ToList());

        var invalidDeviceIds = new HashSet<Guid>();

        foreach (var notification in notifications)
        {
            if (!devicesByUserId.TryGetValue(notification.UserId, out var userDevices) || userDevices.Count == 0)
            {
                continue;
            }

            var result = await _pushNotificationSender.SendAsync(
                new PushNotificationDispatchRequest(
                    notification.TenantId,
                    notification.UserId,
                    notification.Type,
                    notification.Source,
                    notification.Title,
                    notification.Body,
                    notification.Severity,
                    notification.ActionUrl,
                    userDevices.Select(device => new PushNotificationTarget(
                        device.Id,
                        device.Provider,
                        device.Platform,
                        device.DeviceToken)).ToList()),
                cancellationToken);

            foreach (var invalidDeviceId in result.InvalidDeviceIds)
            {
                invalidDeviceIds.Add(invalidDeviceId);
            }
        }

        if (invalidDeviceIds.Count == 0)
        {
            return;
        }

        var invalidDevices = devices
            .Where(x => invalidDeviceIds.Contains(x.Id))
            .ToList();

        foreach (var invalidDevice in invalidDevices)
        {
            invalidDevice.IsActive = false;
            invalidDevice.InvalidatedAtUtc = _clock.UtcNow;
            invalidDevice.LastError = "FCM token rejected by provider.";
        }

        await SaveChangesForTenantAsync(notifications.First().TenantId, cancellationToken);
    }

    private async Task SaveChangesForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_tenantProvider.TryGetCurrentTenantId(out _))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var previousTenantId = _tenantContext.TenantId;
        var previousResolutionSource = _tenantContext.ResolutionSource;

        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = nameof(NotificationService);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _tenantContext.TenantId = previousTenantId;
            _tenantContext.ResolutionSource = previousResolutionSource;
        }
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
