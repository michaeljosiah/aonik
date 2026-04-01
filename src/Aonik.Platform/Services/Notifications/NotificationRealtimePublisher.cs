using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aonik.Platform.Contracts.Models.Notifications;

namespace Aonik.Platform.Services.Notifications;

internal interface INotificationRealtimePublisher
{
    IAsyncEnumerable<NotificationRealtimeEvent> SubscribeAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    ValueTask PublishAsync(
        NotificationRealtimeEvent notificationEvent,
        CancellationToken cancellationToken = default);
}

internal sealed record NotificationRealtimeEvent(
    string Type,
    NotificationResponse Notification,
    int UnreadCountDelta = 0);

internal sealed class NotificationRealtimePublisher : INotificationRealtimePublisher
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<NotificationRealtimeEvent>>> _subscribers = new();

    public async IAsyncEnumerable<NotificationRealtimeEvent> SubscribeAsync(
        Guid tenantId,
        Guid userId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var key = BuildKey(tenantId, userId);
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<NotificationRealtimeEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var subscribers = _subscribers.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Channel<NotificationRealtimeEvent>>());
        subscribers[subscriberId] = channel;

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (channel.Reader.TryRead(out var notificationEvent))
                {
                    yield return notificationEvent;
                }
            }
        }
        finally
        {
            if (_subscribers.TryGetValue(key, out var activeSubscribers))
            {
                activeSubscribers.TryRemove(subscriberId, out _);
                if (activeSubscribers.IsEmpty)
                {
                    _subscribers.TryRemove(key, out _);
                }
            }
        }
    }

    public ValueTask PublishAsync(
        NotificationRealtimeEvent notificationEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(notificationEvent.Notification.TenantId, notificationEvent.Notification.UserId);
        if (!_subscribers.TryGetValue(key, out var subscribers))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var subscriber in subscribers.Values)
        {
            subscriber.Writer.TryWrite(notificationEvent);
        }

        return ValueTask.CompletedTask;
    }

    private static string BuildKey(Guid tenantId, Guid userId)
        => $"{tenantId:N}:{userId:N}";
}
