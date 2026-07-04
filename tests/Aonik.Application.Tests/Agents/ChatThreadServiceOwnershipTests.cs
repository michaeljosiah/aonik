using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Owner-only access enforcement for chat threads (H15): within a tenant, a thread is
/// reachable only by the authenticated user who owns it, and an unresolved user fails closed.
/// </summary>
public class ChatThreadServiceOwnershipTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AgentsDbContext CreateDb()
        => new(
            new DbContextOptionsBuilder<AgentsDbContext>()
                .UseInMemoryDatabase($"Threads_{Guid.NewGuid()}").Options,
            new TestTenantProvider(Tenant));

    private static ChatThread Thread(Guid id, Guid userId, string title) => new()
    {
        Id = id,
        TenantId = Tenant,
        UserId = userId,
        Title = title,
        Status = ChatThreadStatus.Active,
        AgentName = "test",
        LastMessageAt = DateTime.UtcNow,
        MessageCount = 1,
    };

    private static ChatThreadService Service(AgentsDbContext db, Guid currentUser)
        => new(db, new TestTenantProvider(Tenant), new TestCurrentUserProvider(currentUser),
            NullLogger<ChatThreadService>.Instance);

    private static async Task<(AgentsDbContext db, Guid aThread, Guid bThread)> SeedTwoUsersAsync()
    {
        var db = CreateDb();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        db.ChatThreads.AddRange(Thread(a, UserA, "A's thread"), Thread(b, UserB, "B's thread"));
        await db.SaveChangesAsync();
        return (db, a, b);
    }

    [Fact]
    public async Task GetThreadAsync_Should_ReturnOwnThread_ButNotAnotherUsers()
    {
        var (db, aThread, bThread) = await SeedTwoUsersAsync();
        using var _ = db;
        var service = Service(db, UserA);

        (await service.GetThreadAsync(aThread)).Should().NotBeNull();
        (await service.GetThreadAsync(bThread)).Should().BeNull("another user's thread must not be reachable");
    }

    [Fact]
    public async Task ListThreadsAsync_Should_ReturnOnlyTheCurrentUsersThreads()
    {
        var (db, aThread, _) = await SeedTwoUsersAsync();
        using var _ = db;
        var service = Service(db, UserA);

        var threads = await service.ListThreadsAsync();

        threads.Should().ContainSingle().Which.Id.Should().Be(aThread);
    }

    [Fact]
    public async Task ArchiveThreadAsync_Should_RefuseAnotherUsersThread()
    {
        var (db, aThread, bThread) = await SeedTwoUsersAsync();
        using var _ = db;
        var service = Service(db, UserA);

        (await service.ArchiveThreadAsync(bThread)).Should().BeFalse("cannot archive another user's thread");
        (await service.ArchiveThreadAsync(aThread)).Should().BeTrue("owner can archive their own thread");

        var bStillActive = await db.ChatThreads.AsNoTracking().FirstAsync(t => t.Id == bThread);
        bStillActive.Status.Should().Be(ChatThreadStatus.Active, "the refused archive must not have mutated B's thread");
    }

    [Fact]
    public async Task Should_FailClosed_When_NoUserResolved()
    {
        var (db, aThread, _) = await SeedTwoUsersAsync();
        using var _ = db;
        var service = Service(db, Guid.Empty); // resolved-but-empty user id

        (await service.GetThreadAsync(aThread)).Should().BeNull();
        (await service.ListThreadsAsync()).Should().BeEmpty();
        (await service.ArchiveThreadAsync(aThread)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_FailClosed_When_TryGetCurrentUserIdReturnsFalse()
    {
        // The other half of the guard: an unauthenticated request where the provider reports
        // no user at all (TryGet returns false), not just an empty Guid.
        var (db, aThread, _) = await SeedTwoUsersAsync();
        using var _ = db;
        var service = new ChatThreadService(
            db, new TestTenantProvider(Tenant), new NoUserProvider(),
            NullLogger<ChatThreadService>.Instance);

        (await service.GetThreadAsync(aThread)).Should().BeNull();
        (await service.ListThreadsAsync()).Should().BeEmpty();
        (await service.ArchiveThreadAsync(aThread)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_ExcludeAnonymousNullOwnerThread_FromAuthenticatedUser()
    {
        // The exact pre-fix vulnerability: a null-owner (anonymously created) thread must not be
        // reachable by any authenticated user, via get or list.
        using var db = CreateDb();
        var orphan = Guid.NewGuid();
        db.ChatThreads.Add(new ChatThread
        {
            Id = orphan,
            TenantId = Tenant,
            UserId = null,
            Title = "orphan",
            Status = ChatThreadStatus.Active,
            AgentName = "test",
            LastMessageAt = DateTime.UtcNow,
            MessageCount = 1,
        });
        await db.SaveChangesAsync();
        var service = Service(db, UserA);

        (await service.GetThreadAsync(orphan)).Should().BeNull("a null-owner thread is owned by no one");
        (await service.ListThreadsAsync()).Should().BeEmpty("null-owner threads must never appear in a user's list");
    }

    private sealed class NoUserProvider : Aonik.SharedKernel.Abstractions.ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => null;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = Guid.Empty;
            return false;
        }
    }
}
