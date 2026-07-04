using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Process-local store for orchestrator <see cref="AgentSession"/> instances,
/// keyed by session id. Replaces the previous unbounded
/// <c>static ConcurrentDictionary</c> (issue H7): that dictionary never evicted,
/// so under sustained traffic it grew without bound — a slow memory leak that
/// only shows up in production.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately an <b>L1 (in-process) cache only</b>, not the source of
/// truth. Conversation history is already durably persisted per turn to the
/// <c>ChatThread</c>/<c>ChatMessage</c> tables via <c>IChatThreadService</c>; the
/// MAF <see cref="AgentSession"/> is an in-memory optimisation over that record.
/// Cross-node continuity (so a session started on one instance survives a move to
/// another) is a separate, larger follow-up — reachable either by serialising the
/// session to a distributed store or by rehydrating a fresh session from the
/// persisted thread history on a miss — and is intentionally out of scope here.
/// What this type fixes is the concrete, guaranteed bug: unbounded growth.
/// </para>
/// <para>
/// Registered as a singleton so the bound is process-wide while the consuming
/// <c>MasterOrchestratorService</c> stays request-scoped. On eviction the entry is
/// simply released to GC — the same lifecycle the old dictionary gave every session
/// (the MAF session type is not <see cref="IDisposable"/>, so there is nothing to
/// dispose). A returning session id whose entry was evicted gets a fresh session;
/// that is a pre-existing boundary behaviour, unchanged by this store.
/// </para>
/// </remarks>
internal interface IOrchestratorSessionStore
{
    /// <summary>Returns the cached session for <paramref name="sessionId"/>, if present.</summary>
    bool TryGet(string sessionId, [NotNullWhen(true)] out AgentSession? session);

    /// <summary>
    /// Adds <paramref name="session"/> for <paramref name="sessionId"/> and returns it,
    /// or — if another caller already added a session for the same id — returns the
    /// existing one (and drops the redundant <paramref name="session"/>). Mirrors the
    /// atomic get-or-add semantics of the dictionary it replaces.
    /// </summary>
    AgentSession GetOrAdd(string sessionId, AgentSession session);
}

/// <summary>
/// <see cref="MemoryCache"/>-backed <see cref="IOrchestratorSessionStore"/> with a
/// hard entry cap and sliding expiration, so idle sessions are evicted and the
/// working set stays bounded regardless of total traffic.
/// </summary>
internal sealed class InMemoryOrchestratorSessionStore : IOrchestratorSessionStore, IDisposable
{
    // Defaults chosen so an active multi-turn conversation is never evicted mid-use
    // (2h idle window), while a bounded number of sessions can be retained at once.
    private const int DefaultSizeLimit = 10_000;
    private const int DefaultSlidingExpirationMinutes = 120;

    private readonly MemoryCache _cache;
    private readonly TimeSpan _slidingExpiration;
    private readonly ILogger<InMemoryOrchestratorSessionStore> _logger;
    private readonly object _addLock = new();

    public InMemoryOrchestratorSessionStore(
        IConfiguration configuration,
        ILogger<InMemoryOrchestratorSessionStore> logger)
    {
        _logger = logger;

        var sizeLimit = configuration.GetValue<long?>("Agents:Orchestrator:SessionCache:SizeLimit")
            ?? DefaultSizeLimit;
        var slidingMinutes = configuration.GetValue<int?>("Agents:Orchestrator:SessionCache:SlidingExpirationMinutes")
            ?? DefaultSlidingExpirationMinutes;

        _slidingExpiration = TimeSpan.FromMinutes(slidingMinutes);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
    }

    /// <summary>Current number of retained sessions. Exposed for tests that assert the bound.</summary>
    internal long Count => _cache.Count;

    public bool TryGet(string sessionId, [NotNullWhen(true)] out AgentSession? session)
        => _cache.TryGetValue(sessionId, out session) && session is not null;

    public AgentSession GetOrAdd(string sessionId, AgentSession session)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(session);

        // The critical section is only entered on a cache miss (new session), which
        // is rare relative to the per-turn TryGet fast path, so a coarse lock here is
        // cheap and keeps get-or-add atomic (no torn double-insert under a race).
        lock (_addLock)
        {
            if (_cache.TryGetValue(sessionId, out AgentSession? existing) && existing is not null)
            {
                // Lost the race — the caller uses our return value, so the redundant
                // session it handed us is simply dropped (GC reclaims it), exactly as
                // the ConcurrentDictionary.GetOrAdd this replaced did with its loser.
                return existing;
            }

            var entryOptions = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetSlidingExpiration(_slidingExpiration)
                .RegisterPostEvictionCallback(OnEvicted);

            _cache.Set(sessionId, session, entryOptions);
            return session;
        }
    }

    private void OnEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        // Diagnostic only. Nothing to dispose (the MAF session type holds managed
        // state and implements no disposal interface); eviction just drops the
        // reference so the working set stays bounded.
        if (reason is EvictionReason.Expired or EvictionReason.Capacity)
        {
            _logger.LogDebug(
                "Evicted orchestrator session {SessionId} ({Reason})", key, reason);
        }
    }

    public void Dispose() => _cache.Dispose();
}
