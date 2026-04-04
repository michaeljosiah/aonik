using System.Collections.Concurrent;

namespace Aonik.Ai.Services;

internal interface ITextToSpeechRateLimiter
{
    bool TryConsume(Guid tenantId, Guid userId, int maxRequestsPerMinute, out TimeSpan retryAfter);
}

internal sealed class TextToSpeechRateLimiter : ITextToSpeechRateLimiter
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _requests = new(StringComparer.Ordinal);

    public bool TryConsume(Guid tenantId, Guid userId, int maxRequestsPerMinute, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (maxRequestsPerMinute <= 0)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var key = $"{tenantId:N}:{userId:N}";
        var queue = _requests.GetOrAdd(key, static _ => new ConcurrentQueue<DateTime>());

        while (queue.TryPeek(out var timestamp) && now - timestamp >= TimeSpan.FromMinutes(1))
        {
            queue.TryDequeue(out _);
        }

        if (queue.Count >= maxRequestsPerMinute)
        {
            queue.TryPeek(out var first);
            retryAfter = first == default
                ? TimeSpan.FromMinutes(1)
                : TimeSpan.FromMinutes(1) - (now - first);
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }

            return false;
        }

        queue.Enqueue(now);
        return true;
    }
}
