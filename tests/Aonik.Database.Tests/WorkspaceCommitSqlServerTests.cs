using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
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
/// Spec 089 §6 — commits, idempotency, and the compare-and-swap that decides the head.
///
/// <para>
/// This lane, not InMemory, for the same reason as the blob tests: the guarantee <em>is</em> that sequence
/// allocation and head advancement happen in one guarded statement, and <c>ExecuteUpdateAsync</c> does not exist
/// on the InMemory provider. A test that could not issue the statement would assert the shape of the code rather
/// than the property.
/// </para>
/// </summary>
public class WorkspaceCommitSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

    public WorkspaceCommitSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class NoopFileStore : IFileStore
    {
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<FileUploadResult> UploadAsync(
            Guid tenantId, Guid ownerEntityId, Stream fileStream, string fileName, string contentType,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public string GetUrl(string storageKey) => storageKey;

        public Task<StagedBlob> StageAsync(
            Guid tenantId, Stream content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PromoteResult> PromoteAsync(
            StagedBlob staged, string contentKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private WorkspacesDbContext CreateContext(Guid tenantId)
        => new(
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseSqlServer(_db.ConnectionString)
                .Options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static WorkspaceSyncService CreateSync(
        WorkspacesDbContext context, Guid tenantId, IShareGrantReader? grants = null)
    {
        var blobs = new WorkspaceBlobService(
            context, new NoopFileStore(), new TestTenantProvider(tenantId), new TestClock(),
            NullLogger<WorkspaceBlobService>.Instance);

        var possessions = new BlobPossessionService(
            context, new UnmeteredMeter(), new TestTenantProvider(tenantId),
            NullLogger<BlobPossessionService>.Instance);

        return new WorkspaceSyncService(
            context, blobs, grants ?? new NoGrants(), possessions,
            new TestTenantProvider(tenantId), new TestClock(),
            NullLogger<WorkspaceSyncService>.Instance);
    }

    /// <summary>No grants at all — the commit tests are about the head, not about sharing.</summary>
    private sealed class NoGrants : IShareGrantReader
    {
        public Task<bool> HasGrantAsync(
            Guid memberPartyId, string resourceKind, Guid resourceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<ShareGrantDto>> GetActiveGrantsAsync(
            Guid memberPartyId, string resourceKind, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ShareGrantDto>>([]);

        public Task<string?> GetAccessLevelAsync(
            Guid memberPartyId, string resourceKind, Guid resourceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Accepts every claim. Quota is exercised in its own tests; here it would only obscure what the
    /// commit path is doing.
    /// </summary>
    private sealed class UnmeteredMeter : IUsageMeter
    {
        public Task ClaimSlotAsync(
            SubscriberRef subscriber, string meterCode, string holderRef, long weight = 1,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseSlotAsync(
            SubscriberRef subscriber, string meterCode, string holderRef,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<UsageReservationRef> ReserveAsync(
            SubscriberRef subscriber, string meterCode, decimal quantity, string idempotencyKey,
            TimeSpan? holdFor = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UsageCommitResult> CommitAsync(
            Guid reservationId, decimal actualQuantity, UsageSource source,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HasFlagAsync(
            SubscriberRef subscriber, string meterCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static async Task<Workspace> SeedWorkspaceAsync(
        WorkspacesDbContext context, Guid tenantId, Guid ownerPartyId)
    {
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = WorkspaceKinds.World,
            Name = "The Undersong",
            Slug = $"the-undersong-{Guid.NewGuid():N}"[..40],
            OwnerPartyId = ownerPartyId,
            Status = WorkspaceStatuses.Active,
            NextSequence = 1,
        };

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();
        return workspace;
    }

    private static async Task<string> SeedBlobAsync(WorkspacesDbContext context, Guid tenantId, string seed)
    {
        var hash = System.Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed)))
            .ToLowerInvariant();

        context.Blobs.Add(new WorkspaceBlob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentHash = hash,
            StorageKey = WorkspaceBlobService.ContentKeyFor(tenantId, hash),
            SizeBytes = seed.Length,
            RefCount = 0,
        });

        await context.SaveChangesAsync();
        return hash;
    }

    private static CommitRevisionRequest ACommit(
        Guid workspaceId, Guid? parent, params (string Path, string Hash)[] files)
        => new(
            workspaceId,
            Guid.NewGuid(),
            parent,
            [.. files.Select(f => new ManifestEntry(f.Path, f.Hash, 16, "text/plain"))],
            "a commit");

    // ── Fast-forward ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AFirstCommit_Should_FastForwardAndBecomeTheHead()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");

        var result = await CreateSync(context, tenantId)
            .CommitAsync(ACommit(workspace.Id, null, ("scenes/one.md", hash)), owner);

        result.Outcome.Should().Be(CommitOutcome.FastForward);
        result.Sequence.Should().Be(1);

        var after = await context.Workspaces.AsNoTracking().FirstAsync(w => w.Id == workspace.Id);
        after.HeadRevisionId.Should().Be(result.RevisionId);
        after.NextSequence.Should().Be(2, "a sequence is only ever consumed by a successful CAS");
    }

    [SkippableFact]
    public async Task ACommitFromTheHead_Should_FastForward()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var sync = CreateSync(context, tenantId);

        var first = await sync.CommitAsync(ACommit(workspace.Id, null, ("a.md", hash)), owner);
        var second = await sync.CommitAsync(
            ACommit(workspace.Id, first.RevisionId, ("a.md", hash)), owner);

        second.Outcome.Should().Be(CommitOutcome.FastForward);
        second.Sequence.Should().Be(2);
    }

    // ── Divergence ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task TwoCommitsFromTheSameHead_Should_BothPersist_WithTheLoserDiverged()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var sync = CreateSync(context, tenantId);

        var root = await sync.CommitAsync(ACommit(workspace.Id, null, ("a.md", hash)), owner);

        var alice = await sync.CommitAsync(
            ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("b.md", hash)), owner);
        var bob = await sync.CommitAsync(
            ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("c.md", hash)), owner);

        // The acceptance criterion Rev 2 added and Rev 3's CAS could not satisfy: the loser is stored as
        // Diverged rather than vanishing or colliding on the sequence index.
        alice.Outcome.Should().Be(CommitOutcome.FastForward);
        bob.Outcome.Should().Be(CommitOutcome.Diverged);

        bob.Sequence.Should().Be(3, "a diverged revision is a real revision and must be orderable");

        var after = await context.Workspaces.AsNoTracking().FirstAsync(w => w.Id == workspace.Id);
        after.HeadRevisionId.Should().Be(alice.RevisionId, "the loser must not move the head");

        (await context.Revisions.AsNoTracking().CountAsync(r => r.WorkspaceId == workspace.Id))
            .Should().Be(3, "nothing is lost while the human decides");
    }

    [SkippableFact]
    public async Task ACommitNamingAnUnpossessedHash_Should_BeRefused()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var absent = new string('f', 64);

        var result = await CreateSync(context, tenantId)
            .CommitAsync(ACommit(workspace.Id, null, ("a.md", absent)), owner);

        // Upload first, commit second — never the reverse.
        result.MissingHashes.Should().Contain(absent);

        var after = await context.Workspaces.AsNoTracking().FirstAsync(w => w.Id == workspace.Id);
        after.NextSequence.Should().Be(1, "an obviously incomplete commit consumes no sequence");
    }

    // ── Idempotency ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ARetriedCommit_Should_ReplayTheOriginalOutcome()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var sync = CreateSync(context, tenantId);

        var request = ACommit(workspace.Id, null, ("a.md", hash));

        var first = await sync.CommitAsync(request, owner);
        var retry = await sync.CommitAsync(request, owner);

        retry.Outcome.Should().Be(CommitOutcome.Replayed);
        retry.RevisionId.Should().Be(first.RevisionId);

        (await context.Revisions.AsNoTracking().CountAsync(r => r.WorkspaceId == workspace.Id))
            .Should().Be(1, "a timeout followed by a retry must not create a second revision");
    }

    [SkippableFact]
    public async Task TheSameCommitId_ForADifferentTree_Should_BeRefusedNotReplayed()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var other = await SeedBlobAsync(context, tenantId, "act two");
        var sync = CreateSync(context, tenantId);

        var request = ACommit(workspace.Id, null, ("a.md", hash));
        await sync.CommitAsync(request, owner);

        // The author kept working after the timeout, so the client correctly reuses its CommitId and
        // rebuilds the manifest from a tree that has since changed. Replaying would tell it the NEW tree
        // is committed when it is not, and the next pull would treat those edits as absent — work lost
        // silently with a success response on the record.
        var changed = request with
        {
            Manifest = [new ManifestEntry("a.md", other, 16, "text/plain")],
        };

        var act = async () => await sync.CommitAsync(changed, owner);

        await act.Should().ThrowAsync<CommitIdReusedException>();
    }

    [SkippableFact]
    public async Task AManifestInADifferentOrder_Should_StillBeATrueRetry()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var a = await SeedBlobAsync(context, tenantId, "act one");
        var b = await SeedBlobAsync(context, tenantId, "act two");
        var sync = CreateSync(context, tenantId);

        var request = ACommit(workspace.Id, null, ("a.md", a), ("b.md", b));
        await sync.CommitAsync(request, owner);

        // Two clients enumerating the same tree in different orders describe the same tree. Order
        // sensitivity would turn an honest retry into a 409.
        var reordered = request with
        {
            Manifest = [.. request.Manifest.Reverse()],
        };

        (await sync.CommitAsync(reordered, owner)).Outcome.Should().Be(CommitOutcome.Replayed);
    }

    // ── Resolution ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AcceptingADivergentRevision_Should_AdvanceTheHeadThroughANewRevision()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var sync = CreateSync(context, tenantId);

        var root = await sync.CommitAsync(ACommit(workspace.Id, null, ("a.md", hash)), owner);
        await sync.CommitAsync(ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("b.md", hash)), owner);
        var diverged = await sync.CommitAsync(
            ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("c.md", hash)), owner);

        (await sync.ResolveAsync(diverged.RevisionId, owner, DivergenceResolution.Accept)).Should().BeTrue();

        var after = await context.Workspaces.AsNoTracking().FirstAsync(w => w.Id == workspace.Id);

        // History is never rewritten. Repointing the head at a revision whose parent is not its
        // predecessor would make the chain a lie and break every history read.
        after.HeadRevisionId.Should().NotBe(diverged.RevisionId);

        var head = await context.Revisions.AsNoTracking().FirstAsync(r => r.Id == after.HeadRevisionId);
        head.Sequence.Should().Be(4);

        var resolved = await context.Revisions.AsNoTracking().FirstAsync(r => r.Id == diverged.RevisionId);
        resolved.State.Should().Be(RevisionStates.Accepted);

        var manifest = await sync.GetManifestAsync(workspace.Id, owner);
        manifest.Select(m => m.Path).Should().BeEquivalentTo(["a.md", "c.md"],
            "the new head carries the accepted tree");
    }

    [SkippableFact]
    public async Task RejectingADivergentRevision_Should_NotReleaseItsBlobsInline()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var sync = CreateSync(context, tenantId);

        var root = await sync.CommitAsync(ACommit(workspace.Id, null, ("a.md", hash)), owner);
        await sync.CommitAsync(ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("b.md", hash)), owner);
        var diverged = await sync.CommitAsync(
            ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("c.md", hash)), owner);

        var before = (await context.Blobs.AsNoTracking().FirstAsync(b => b.ContentHash == hash)).RefCount;

        await sync.ResolveAsync(diverged.RevisionId, owner, DivergenceResolution.Reject);

        // §7.1 keeps a rejected revision for a retention window so the rejection is undoable, and the
        // sweeper releases afterwards. Releasing inline would make "undo" mean "re-upload".
        (await context.Blobs.AsNoTracking().FirstAsync(b => b.ContentHash == hash))
            .RefCount.Should().Be(before);

        (await context.Revisions.AsNoTracking().FirstAsync(r => r.Id == diverged.RevisionId))
            .State.Should().Be(RevisionStates.Rejected);
    }

    [SkippableFact]
    public async Task ResolvingTwice_Should_NotActTwice()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");
        var sync = CreateSync(context, tenantId);

        var root = await sync.CommitAsync(ACommit(workspace.Id, null, ("a.md", hash)), owner);
        await sync.CommitAsync(ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("b.md", hash)), owner);
        var diverged = await sync.CommitAsync(
            ACommit(workspace.Id, root.RevisionId, ("a.md", hash), ("c.md", hash)), owner);

        (await sync.ResolveAsync(diverged.RevisionId, owner, DivergenceResolution.Accept)).Should().BeTrue();

        var headAfterFirst = (await context.Workspaces.AsNoTracking()
            .FirstAsync(w => w.Id == workspace.Id)).HeadRevisionId;

        (await sync.ResolveAsync(diverged.RevisionId, owner, DivergenceResolution.Accept)).Should().BeFalse();

        (await context.Workspaces.AsNoTracking().FirstAsync(w => w.Id == workspace.Id))
            .HeadRevisionId.Should().Be(headAfterFirst);
    }

    // ── Access ───────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ANonOwner_Should_NotCommit()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = await SeedWorkspaceAsync(context, tenantId, owner);
        var hash = await SeedBlobAsync(context, tenantId, "act one");

        // Until P5 wires grants the rule is owner-or-nothing, which is stricter than the finished one
        // rather than looser. A stub returning Write for everyone would leave the endpoint open in the
        // window between phases — §8.1's vulnerability arriving by a different route.
        var act = async () => await CreateSync(context, tenantId)
            .CommitAsync(ACommit(workspace.Id, null, ("a.md", hash)), Guid.NewGuid());

        await act.Should().ThrowAsync<WorkspaceAccessDeniedException>();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
