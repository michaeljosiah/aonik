using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Abstractions.Subscriptions;
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
/// Spec 089 §5.1 — the race between sweeping and referencing, in the only lane that can show it.
///
/// <para>
/// Both halves are single <c>ExecuteUpdateAsync</c> statements, which the InMemory provider does not implement
/// at all. That is not a testing inconvenience: the guarantee <em>is</em> that the read and the write happen in
/// one statement, so a test that could not issue one would be asserting the shape of the code rather than the
/// property it exists for.
/// </para>
/// </summary>
public class WorkspaceBlobSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc);

    public WorkspaceBlobSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    /// <summary>Records what the sweeper actually removed from storage.</summary>
    private sealed class RecordingFileStore : IFileStore
    {
        public List<string> Deleted { get; } = [];
        public bool DeleteThrows { get; set; }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (DeleteThrows)
            {
                throw new InvalidOperationException("storage unavailable");
            }

            Deleted.Add(storageKey);
            return Task.CompletedTask;
        }

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

    private static WorkspaceBlobService CreateBlobService(
        WorkspacesDbContext context, Guid tenantId, IFileStore fileStore)
        => new(context, fileStore, new TestTenantProvider(tenantId), new TestClock(),
            NullLogger<WorkspaceBlobService>.Instance);

    private static WorkspaceBlobSweeper CreateSweeper(
        WorkspacesDbContext context, Guid tenantId, IFileStore fileStore, WorkspaceOptions? options = null)
        => new(context, fileStore, new TestTenantProvider(tenantId), new TestClock(),
            Microsoft.Extensions.Options.Options.Create(options ?? new WorkspaceOptions()),
            NullLogger<WorkspaceBlobSweeper>.Instance);

    private static async Task<WorkspaceBlob> SeedBlobAsync(
        WorkspacesDbContext context, Guid tenantId, int refCount, DateTime createdAt, bool deleting = false)
    {
        var hash = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant().PadRight(64, '0')[..64];

        var blob = new WorkspaceBlob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentHash = hash,
            StorageKey = WorkspaceBlobService.ContentKeyFor(tenantId, hash),
            SizeBytes = 4096,
            RefCount = refCount,
            IsDeleting = deleting,
            CreatedAt = createdAt,
        };

        context.Blobs.Add(blob);
        await context.SaveChangesAsync();

        // CreatedAt is stamped by the audit interceptor, so the seeded value has to be forced back for
        // the grace-period predicate to be exercisable at all.
        await context.Blobs.Where(b => b.Id == blob.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.CreatedAt, createdAt));

        return blob;
    }

    // ── The claim ────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnUnreferencedBlob_Should_BeDeletedFromStorageAndTheDatabase()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blob = await SeedBlobAsync(context, tenantId, refCount: 0, createdAt: Now.AddDays(-2));
        var store = new RecordingFileStore();

        var summary = await CreateSweeper(context, tenantId, store).SweepAsync();

        summary.Deleted.Should().Be(1);
        store.Deleted.Should().ContainSingle().Which.Should().Be(blob.StorageKey);
        (await context.Blobs.AsNoTracking().AnyAsync(b => b.Id == blob.Id)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task AReferencedBlob_Should_NotBeSwept()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedBlobAsync(context, tenantId, refCount: 1, createdAt: Now.AddDays(-2));
        var store = new RecordingFileStore();

        // Deleting bytes a revision still names destroys data silently, and it is found much later by
        // a user opening an old revision to an empty tree.
        (await CreateSweeper(context, tenantId, store).SweepAsync()).Deleted.Should().Be(0);
        store.Deleted.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task AReferenceLandingFirst_Should_MakeTheSweeperAbandonTheDeletion()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blob = await SeedBlobAsync(context, tenantId, refCount: 0, createdAt: Now.AddDays(-2));
        var store = new RecordingFileStore();

        // The race, made deterministic: the reference lands between the sweeper selecting a candidate
        // and claiming it. The claim still succeeds — its RefCount read was already stale — which is
        // exactly why there is a re-check under the claim.
        await CreateBlobService(context, tenantId, store).AddReferencesAsync([blob.ContentHash]);

        var summary = await CreateSweeper(context, tenantId, store).SweepAsync();

        summary.Deleted.Should().Be(0);
        store.Deleted.Should().BeEmpty("the bytes a live manifest names must survive the sweep");

        var after = await context.Blobs.AsNoTracking().FirstAsync(b => b.Id == blob.Id);
        after.IsDeleting.Should().BeFalse("an abandoned claim is released, not left blocking references");
        after.RefCount.Should().Be(1);
    }

    [SkippableFact]
    public async Task AClaimedBlob_Should_BeReportedMissingRatherThanReferenced()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blob = await SeedBlobAsync(
            context, tenantId, refCount: 0, createdAt: Now.AddDays(-2), deleting: true);

        var service = CreateBlobService(context, tenantId, new RecordingFileStore());
        var subscriber = new SubscriberRef(SubscriberKinds.Party, Guid.NewGuid());

        // Possession, so the hash is reachable by every route EXCEPT the deletion claim — otherwise
        // this would pass for the wrong reason.
        context.Possessions.Add(new BlobPossession
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            SubscriberKind = subscriber.Kind, SubscriberId = subscriber.Id,
            ContentHash = blob.ContentHash, SizeBytes = blob.SizeBytes, WorkspaceCount = 0,
        });
        await context.SaveChangesAsync();

        // The other half of the mechanism. A commit that referenced this would produce a manifest
        // pointing at bytes the sweeper is about to remove; the safe branch for the client is to
        // upload again, which costs one redundant transfer and can never dangle.
        (await service.FindMissingAsync(subscriber, Guid.NewGuid(), [blob.ContentHash]))
            .Should().Contain(blob.ContentHash);
        (await service.AddReferencesAsync([blob.ContentHash])).Should().Contain(blob.ContentHash);

        (await context.Blobs.AsNoTracking().FirstAsync(b => b.Id == blob.Id))
            .RefCount.Should().Be(0, "a claimed blob must not accumulate references it will lose");
    }

    [SkippableFact]
    public async Task AFailedStorageDelete_Should_LeaveTheClaimInPlace()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blob = await SeedBlobAsync(context, tenantId, refCount: 0, createdAt: Now.AddDays(-2));
        var store = new RecordingFileStore { DeleteThrows = true };

        var summary = await CreateSweeper(context, tenantId, store).SweepAsync();

        summary.Deleted.Should().Be(0);

        // Clearing the claim would offer these bytes back to a referencing client while storage still
        // holds them under a key we just failed to remove.
        var after = await context.Blobs.AsNoTracking().FirstAsync(b => b.Id == blob.Id);
        after.IsDeleting.Should().BeTrue();
    }

    // ── The grace period ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task AFreshlyUploadedBlob_Should_NotBeSweptBeforeItsCommit()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedBlobAsync(context, tenantId, refCount: 0, createdAt: Now.AddMinutes(-5));
        var store = new RecordingFileStore();

        // Storing is not referencing, so every upload passes through a legitimate window at zero while
        // the client assembles the manifest that will name it. Sweeping eagerly deletes content out
        // from under a commit in progress.
        (await CreateSweeper(context, tenantId, store).SweepAsync()).Deleted.Should().Be(0);
    }

    // ── Reference counting ───────────────────────────────────────────────

    [SkippableFact]
    public async Task ReleasingMoreThanHeld_Should_FloorAtZero()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blob = await SeedBlobAsync(context, tenantId, refCount: 1, createdAt: Now.AddDays(-2));
        var service = CreateBlobService(context, tenantId, new RecordingFileStore());

        await service.ReleaseReferencesAsync([blob.ContentHash, blob.ContentHash, blob.ContentHash]);

        // A negative count makes a blob permanently unsweepable, and the bug is invisible until a
        // storage bill arrives.
        (await context.Blobs.AsNoTracking().FirstAsync(b => b.Id == blob.Id))
            .RefCount.Should().Be(0);
    }

    [SkippableFact]
    public async Task TwoRevisionsNamingOneBlob_Should_KeepTheBytesWhenOneIsDeleted()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var blob = await SeedBlobAsync(context, tenantId, refCount: 0, createdAt: Now.AddDays(-2));
        var store = new RecordingFileStore();
        var service = CreateBlobService(context, tenantId, store);

        await service.AddReferencesAsync([blob.ContentHash, blob.ContentHash]);
        await service.ReleaseReferencesAsync([blob.ContentHash]);

        // Acceptance criterion 4. An unchanged sheet is named by every revision that did not touch it;
        // deleting one revision must not empty the others.
        (await CreateSweeper(context, tenantId, store).SweepAsync()).Deleted.Should().Be(0);
        store.Deleted.Should().BeEmpty();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
