using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Notifications;

internal sealed class NotificationDeviceService : INotificationDeviceService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;

    public NotificationDeviceService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
    }

    public async Task<NotificationDeviceResponse> RegisterCurrentUserDeviceAsync(
        RegisterNotificationDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Current user context missing.");
        }

        var provider = NormalizeRequired(request.Provider, nameof(request.Provider));
        var platform = NormalizeRequired(request.Platform, nameof(request.Platform));
        var deviceToken = NormalizeRequired(request.DeviceToken, nameof(request.DeviceToken));
        var now = _clock.UtcNow;

        var notificationDevice = await _dbContext.NotificationDevices
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.Provider == provider
                    && x.Platform == platform
                    && x.DeviceToken == deviceToken,
                cancellationToken);

        if (notificationDevice is null)
        {
            notificationDevice = new NotificationDevice
            {
                TenantId = tenantId,
                UserId = userId,
                Provider = provider,
                Platform = platform,
                DeviceToken = deviceToken,
                IsActive = true,
                LastSeenAtUtc = now
            };

            _dbContext.NotificationDevices.Add(notificationDevice);
        }
        else
        {
            notificationDevice.UserId = userId;
            notificationDevice.IsActive = true;
            notificationDevice.LastSeenAtUtc = now;
            notificationDevice.InvalidatedAtUtc = null;
            notificationDevice.LastError = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationDeviceResponse(
            notificationDevice.Id,
            notificationDevice.Provider,
            notificationDevice.Platform,
            notificationDevice.LastSeenAtUtc);
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return normalized;
    }
}
