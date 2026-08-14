using System.Text;

using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;
using Aonik.Workspaces.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 091 §6 — negotiation is scoped to the caller, and a content hash is not a bearer token.
///
/// <para>
/// <strong>Tenant scope alone is not sufficient</strong> (089 §12). Accepting any hash with a blob in the tenant
/// is fine when a tenant is one customer and wrong for Arke Kids, where one tenant holds many unrelated families.
/// Guessing is not required either — hashes of shared or previously-seen content are knowable, and a match turns
/// into a read.
/// </para>
///
/// <para>
/// <see cref="AHashHeldByAnotherSubscriber_Should_BeMissing_AndTheUploadShould_EstablishPossession"/> is the
/// end-to-end criterion the spec names, because it is the one an implementation is most likely to "optimise" back
/// into a leak.
/// </para>
/// </summary>
public class WorkspaceNegotiationSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    public WorkspaceNegotiationSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    /// <summary>Keeps objects by key, so <c>AlreadyPresent</c> can actually be observed.</summary>
    private sealed class InMemoryFileStore : IFileStore
    {
        public Dictionary<string, byte[]> Objects { get; } = [];

        public Task<StagedBlob> StageAsync(
            Guid tenantId, Stream content, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            var bytes = buffer.ToArray();

            var key = $"staging/{Guid.NewGuid():N}";
            Objects[key] = bytes;

            var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

            return Task.FromResult(new StagedBlob(tenantId, hash, bytes.Length, key));
        }

        public Task<PromoteResult> PromoteAsync(
            StagedBlob staged, string contentKey, CancellationToken cancellationToken = default)
        {
            if (Objects.ContainsKey(contentKey))
            {
                Objects.Remove(staged.TempKey);
                return Task.FromResult(
                    new PromoteResult(PromoteOutcome.AlreadyPresent, contentKey, staged.SizeBytes));
            }

            Objects[contentKey] = Objects[staged.TempKey];
            Objects.Remove(staged.TempKey);

            return Task.FromResult(new PromoteResult(PromoteOutcome.Stored, contentKey, staged.SizeBytes));
        }

        public Task<FileUploadResult> UploadAsync(
            Guid tenantId, Guid ownerEntityId, Stream fileStream, string fileName, string contentType,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(
                Objects.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            Objects.Remove(storageKey);
            return Task.CompletedTask;
        }

        public string GetUrl(string storageKey) => storageKey;
    }

    private WorkspacesDbContext CreateContext(Guid tenantId)
        => new(
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseSqlServer(_db.ConnectionString)
                .Options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static WorkspaceBlobService CreateBlobs(
        WorkspacesDbContext context, Guid tenantId, IFileStore store)
        => new(context, store, new TestTenantProvider(tenantId), new TestClock(),
            NullLogger<WorkspaceBlobService>.Instance);

    private static SubscriberRef Party(Guid id) => new(SubscriberKinds.Party, id);

    private static Stream Content(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static string HashOf(string text)
        => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    // ── The oracle ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AHashHeldByAnotherSubscriber_Should_BeMissing_AndTheUploadShould_EstablishPossession()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var alice = Party(Guid.NewGuid());
        var bob = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var blobs = CreateBlobs(context, tenantId, store);

        const string text = "the undersong, act one";
        var hash = HashOf(text);

        // Alice uploads. The bytes are now physically present in the tenant.
        await blobs.StoreAsync(alice, Content(text));

        // Bob happens to know the hash. Answering "present" would leak that Alice holds it AND leave
        // Bob deadlocked — told the server has the bytes, then told the bytes are missing at commit,
        // with no action that resolves it.
        (await blobs.FindMissingAsync(bob, Guid.NewGuid(), [hash]))
            .Should().Contain(hash, "a hash is a name, not an authorisation");

        // The upload that closes the loop. Promote reports AlreadyPresent — the physical object is
        // reused — but Bob supplied the bytes, so possession is established.
        var stored = await blobs.StoreAsync(bob, Content(text));
        stored.AlreadyPresent.Should().BeTrue("dedupe stays physical");

        store.Objects.Values.Should().ContainSingle(
            "the saving is preserved: one physical object for two subscribers");

        // And now negotiation and commit agree, which is what makes the protocol terminate.
        (await blobs.FindMissingAsync(bob, Guid.NewGuid(), [hash])).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task AHashTheCallerUploaded_Should_NotBeMissing()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var alice = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var blobs = CreateBlobs(context, tenantId, new InMemoryFileStore());

        const string text = "a reference image";
        await blobs.StoreAsync(alice, Content(text));

        // The common case, and the one where the redundant upload never happens.
        (await blobs.FindMissingAsync(alice, Guid.NewGuid(), [HashOf(text)])).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task AHashNobodyHolds_Should_BeMissing()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blobs = CreateBlobs(context, tenantId, new InMemoryFileStore());
        var absent = HashOf("never uploaded");

        // The same answer a caller gets for a hash held by someone else — which is the point. The two
        // cases must be indistinguishable, or the distinction IS the oracle.
        (await blobs.FindMissingAsync(Party(Guid.NewGuid()), Guid.NewGuid(), [absent]))
            .Should().Contain(absent);
    }

    // ── Reachable through a workspace ────────────────────────────────────

    [SkippableFact]
    public async Task AHashInAWorkspaceTheCallerOwns_Should_NotBeMissing()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blobs = CreateBlobs(context, tenantId, new InMemoryFileStore());

        var hash = HashOf("act two");
        var workspaceId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();

        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId, TenantId = tenantId, Kind = WorkspaceKinds.World,
            Name = "The Undersong", Slug = $"u-{Guid.NewGuid():N}"[..20],
            OwnerPartyId = owner, Status = WorkspaceStatuses.Active, NextSequence = 2,
        });
        context.Revisions.Add(new WorkspaceRevision
        {
            Id = revisionId, TenantId = tenantId, WorkspaceId = workspaceId, Sequence = 1,
            CommitId = Guid.NewGuid(), RequestHash = "x", AuthorPartyId = owner,
            State = RevisionStates.FastForward, CommittedAt = Now,
        });
        context.Files.Add(new WorkspaceFile
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RevisionId = revisionId,
            Path = "a.md", ContentHash = hash, SizeBytes = 16,
        });
        await context.SaveChangesAsync();

        // The second route to possession: already reachable through a workspace they can read. Making
        // them re-upload content their own world already names would be a pointless transfer.
        (await blobs.FindMissingAsync(Party(Guid.NewGuid()), owner, [hash])).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task AHashInSomebodyElsesWorkspace_Should_BeMissing()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blobs = CreateBlobs(context, tenantId, new InMemoryFileStore());

        var hash = HashOf("act three");
        var workspaceId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();

        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId, TenantId = tenantId, Kind = WorkspaceKinds.World,
            Name = "Copper Saints", Slug = $"c-{Guid.NewGuid():N}"[..20],
            OwnerPartyId = owner, Status = WorkspaceStatuses.Active, NextSequence = 2,
        });
        context.Revisions.Add(new WorkspaceRevision
        {
            Id = revisionId, TenantId = tenantId, WorkspaceId = workspaceId, Sequence = 1,
            CommitId = Guid.NewGuid(), RequestHash = "x", AuthorPartyId = owner,
            State = RevisionStates.FastForward, CommittedAt = Now,
        });
        context.Files.Add(new WorkspaceFile
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RevisionId = revisionId,
            Path = "a.md", ContentHash = hash, SizeBytes = 16,
        });
        await context.SaveChangesAsync();

        // Same tenant, different family. This is exactly the Arke Kids case tenant scoping got wrong.
        (await blobs.FindMissingAsync(Party(Guid.NewGuid()), stranger, [hash]))
            .Should().Contain(hash);
    }

    // ── Nothing changed ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnUnchangedWorkspace_Should_ReportNothingMissing()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var alice = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var blobs = CreateBlobs(context, tenantId, store);

        var texts = Enumerable.Range(0, 25).Select(i => $"take-{i}").ToList();

        foreach (var text in texts)
        {
            await blobs.StoreAsync(alice, Content(text));
        }

        var transfersBefore = store.Objects.Count;

        // Two round trips before any byte moves, and the second is empty when nothing changed — which
        // is what lets a large unchanged workspace sync with zero blob transfers.
        (await blobs.FindMissingAsync(alice, Guid.NewGuid(), [.. texts.Select(HashOf)]))
            .Should().BeEmpty();

        store.Objects.Count.Should().Be(transfersBefore, "negotiation costs no bytes");
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
