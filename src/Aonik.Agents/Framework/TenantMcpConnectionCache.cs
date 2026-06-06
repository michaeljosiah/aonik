using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Aonik.Agents.Framework;

/// <summary>A live MCP connection plus the tools discovered from it.</summary>
internal sealed class CachedMcpConnection
{
    public required McpClient Client { get; init; }
    public required IReadOnlyList<AITool> Tools { get; init; }
}

/// <summary>
/// Caches tenant MCP connections per <c>(tenantId, serverId, credentialVersion)</c> (Spec 033 §8.3).
/// The discovered <see cref="McpClient"/> is kept alive because its tools call back to it; a
/// credential rotation or edit changes the key (new <c>credentialVersion</c>) and
/// <see cref="InvalidateAsync"/> disposes the stale client. Singleton: the cache outlives requests
/// so only the first agent build for a server pays the connection cost; it is never shared across
/// tenants because the tenant id is part of the key.
/// </summary>
internal sealed class TenantMcpConnectionCache : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<CachedMcpConnection>>> _entries = new(StringComparer.Ordinal);
    private readonly ILogger<TenantMcpConnectionCache> _logger;

    public TenantMcpConnectionCache(ILogger<TenantMcpConnectionCache> logger)
    {
        _logger = logger;
    }

    public static string KeyPrefix(Guid tenantId, Guid serverId) => $"{tenantId:N}:{serverId:N}:";

    public static string Key(Guid tenantId, Guid serverId, int credentialVersion) =>
        $"{KeyPrefix(tenantId, serverId)}{credentialVersion}";

    /// <summary>Get the cached connection for <paramref name="cacheKey"/>, or run <paramref name="connect"/> once.</summary>
    public async Task<CachedMcpConnection> GetOrConnectAsync(string cacheKey, Func<Task<CachedMcpConnection>> connect)
    {
        var lazy = _entries.GetOrAdd(cacheKey, _ => new Lazy<Task<CachedMcpConnection>>(connect));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            // Don't cache a failed connection — let the next build retry.
            _entries.TryRemove(cacheKey, out _);
            throw;
        }
    }

    /// <summary>Dispose and drop every cached connection for a server (any credential version).</summary>
    public async Task InvalidateAsync(Guid tenantId, Guid serverId)
    {
        var prefix = KeyPrefix(tenantId, serverId);
        foreach (var kvp in _entries.ToArray())
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal) && _entries.TryRemove(kvp.Key, out var lazy))
            {
                await DisposeEntryAsync(lazy).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _entries.ToArray())
        {
            if (_entries.TryRemove(kvp.Key, out var lazy))
            {
                await DisposeEntryAsync(lazy).ConfigureAwait(false);
            }
        }
    }

    private async Task DisposeEntryAsync(Lazy<Task<CachedMcpConnection>> lazy)
    {
        try
        {
            if (lazy.IsValueCreated)
            {
                var connection = await lazy.Value.ConfigureAwait(false);
                await connection.Client.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing cached tenant MCP connection");
        }
    }
}
