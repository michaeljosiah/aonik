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
        Guid tenantId,
        Guid threadId,
        Func<CancellationToken, Task<IReadOnlyList<AguiMessage>>> factory,
        CancellationToken cancellationToken = default)
    {
        var cacheMiss = false;

        var snapshot = await _cache.GetOrSetAsync(
            BuildCacheKey(tenantId, threadId),
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
        Guid tenantId,
        Guid threadId,
        IReadOnlyList<AguiMessage> messages,
        CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(BuildCacheKey(tenantId, threadId), token: cancellationToken);

        _ = await _cache.GetOrSetAsync(
            BuildCacheKey(tenantId, threadId),
            _ => Task.FromResult(new ChatThreadHistorySnapshot(CloneMessages(messages))),
            EntryOptions,
            cancellationToken);
    }

    public async Task AppendAsync(
        Guid tenantId,
        Guid threadId,
        AguiMessage message,
        CancellationToken cancellationToken = default)
    {
        var lookup = await GetOrLoadAsync(
            tenantId,
            threadId,
            _ => Task.FromResult<IReadOnlyList<AguiMessage>>([]),
            cancellationToken);

        var updated = lookup.Snapshot.Messages.ToList();
        updated.Add(CloneMessage(message));
        await StoreAsync(tenantId, threadId, updated, cancellationToken);
    }

    /// <summary>
    /// Build the FusionCache key. Tenant-prefixed so two tenants whose
    /// thread GUIDs ever overlap (e.g. via a future cross-tenant import
    /// migration) cannot read or overwrite each other's history snapshot.
    /// The <c>v1</c> segment lets us bump the cache schema without
    /// touching every key by hand.
    /// </summary>
    private static string BuildCacheKey(Guid tenantId, Guid threadId)
        => $"agui:thread-history:v1:{tenantId:N}:{threadId:N}";

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
