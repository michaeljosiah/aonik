using Aonik.Agents.Framework;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Covers the bounded, evictable orchestrator session store that replaced the
/// unbounded static dictionary (issue H7): idempotent get-or-add under a race,
/// and — the crux of the fix — that the working set stays bounded under load.
/// </summary>
public class OrchestratorSessionStoreTests
{
    private static InMemoryOrchestratorSessionStore CreateStore(int sizeLimit = 10_000)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Orchestrator:SessionCache:SizeLimit"] = sizeLimit.ToString(),
                ["Agents:Orchestrator:SessionCache:SlidingExpirationMinutes"] = "120",
            })
            .Build();

        return new InMemoryOrchestratorSessionStore(
            config, NullLogger<InMemoryOrchestratorSessionStore>.Instance);
    }

    [Fact]
    public void TryGet_Should_ReturnFalse_When_SessionNotPresent()
    {
        using var store = CreateStore();

        store.TryGet("missing", out var session).Should().BeFalse();
        session.Should().BeNull();
    }

    [Fact]
    public void GetOrAdd_Should_StoreAndReturnSession_When_New()
    {
        using var store = CreateStore();
        var session = new TestSession();

        var added = store.GetOrAdd("s1", session);

        added.Should().BeSameAs(session);
        store.TryGet("s1", out var fetched).Should().BeTrue();
        fetched.Should().BeSameAs(session);
    }

    [Fact]
    public void GetOrAdd_Should_ReturnWinner_When_SameIdAddedTwice()
    {
        using var store = CreateStore();
        var winner = new TestSession();
        var loser = new TestSession();

        var first = store.GetOrAdd("s1", winner);
        var second = store.GetOrAdd("s1", loser);

        first.Should().BeSameAs(winner);
        second.Should().BeSameAs(winner, "a second add for the same id must return the existing session");
        store.TryGet("s1", out var fetched).Should().BeTrue();
        fetched.Should().BeSameAs(winner);
    }

    [Fact]
    public void GetOrAdd_Should_BoundWorkingSet_When_FarMoreSessionsThanLimit()
    {
        const int sizeLimit = 10;
        using var store = CreateStore(sizeLimit);

        // Insert far more distinct sessions than the cap. The old static dictionary
        // would retain all 500 forever (the H7 leak); the bounded cache must not.
        for (var i = 0; i < 500; i++)
            store.GetOrAdd($"s{i}", new TestSession());

        // MemoryCache runs overcapacity compaction on a background thread, so poll
        // briefly for convergence rather than asserting synchronously.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.Count > sizeLimit && DateTime.UtcNow < deadline)
            Thread.Sleep(25);

        store.Count.Should().BeLessThanOrEqualTo(
            sizeLimit, "the cache must enforce its size bound instead of growing without limit");
    }

    [Fact]
    public void Dispose_Should_NotThrow()
    {
        var store = CreateStore();
        store.GetOrAdd("s1", new TestSession());

        var act = () => store.Dispose();

        act.Should().NotThrow();
    }

    // The real MAF AgentSession / ChatClientAgentSession implement no disposal
    // interface and hold managed state only, so the stub needs nothing beyond the
    // base type — the store never disposes it, matching production.
    private sealed class TestSession : AgentSession;
}
