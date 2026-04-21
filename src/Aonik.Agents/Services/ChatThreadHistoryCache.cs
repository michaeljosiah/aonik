using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Agents.Services;

internal sealed class ChatThreadHistoryCache : IChatThreadHistoryCache
{
    private static readonly FusionCacheEntryOptions EntryOptions = new(TimeSpan.FromMinutes(30))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(1),
    };

    private readonly IFusionCache _cache;

    public ChatThreadHistoryCache(IFusionCache cache)
    {
        _cache = cache;
    }

    public async Task<ChatThreadHistoryCacheLookup> GetOrLoadAsync(
        Guid threadId,
        Func<CancellationToken, Task<IReadOnlyList<AguiMessage>>> factory,
        CancellationToken cancellationToken = default)
    {
        var cacheMiss = false;

        var snapshot = await _cache.GetOrSetAsync(
            BuildCacheKey(threadId),
            async ct =>
            {
                cacheMiss = true;
                var messages = await factory(ct);
                return new ChatThreadHistorySnapshot(CloneMessages(messages));
            },
            EntryOptions,
            cancellationToken);

        return new ChatThreadHistoryCacheLookup(
            snapshot ?? new ChatThreadHistorySnapshot([]),
            IsCacheHit: !cacheMiss);
    }

    public async Task StoreAsync(
        Guid threadId,
        IReadOnlyList<AguiMessage> messages,
        CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(BuildCacheKey(threadId), token: cancellationToken);

        _ = await _cache.GetOrSetAsync(
            BuildCacheKey(threadId),
            _ => Task.FromResult(new ChatThreadHistorySnapshot(CloneMessages(messages))),
            EntryOptions,
            cancellationToken);
    }

    public async Task AppendAsync(
        Guid threadId,
        AguiMessage message,
        CancellationToken cancellationToken = default)
    {
        var lookup = await GetOrLoadAsync(
            threadId,
            _ => Task.FromResult<IReadOnlyList<AguiMessage>>([]),
            cancellationToken);

        var updated = lookup.Snapshot.Messages.ToList();
        updated.Add(CloneMessage(message));
        await StoreAsync(threadId, updated, cancellationToken);
    }

    private static string BuildCacheKey(Guid threadId)
        => $"agui:thread-history:v1:{threadId:N}";

    private static IReadOnlyList<AguiMessage> CloneMessages(IReadOnlyList<AguiMessage> messages)
        => messages.Select(CloneMessage).ToList();

    private static AguiMessage CloneMessage(AguiMessage message)
        => new()
        {
            Id = message.Id,
            Role = message.Role,
            Content = message.Content,
            Name = message.Name,
            ToolCallId = message.ToolCallId,
            Error = message.Error,
            EncryptedContent = message.EncryptedContent,
            EncryptedValue = message.EncryptedValue,
            ActivityType = message.ActivityType,
            ToolCalls = message.ToolCalls?.Select(tc => new AguiToolCall
            {
                Id = tc.Id,
                Type = tc.Type,
                EncryptedValue = tc.EncryptedValue,
                Function = tc.Function is null
                    ? null
                    : new AguiFunctionCall
                    {
                        Name = tc.Function.Name,
                        Arguments = tc.Function.Arguments,
                    }
            }).ToList(),
        };
}
