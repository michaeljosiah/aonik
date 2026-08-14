using System.Security.Cryptography;
using System.Text;

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
/// Spec 091 §7 — resumable multipart upload, and the two bounds it enforces.
///
/// <para>
/// <strong>Resume between blobs is not resume within one.</strong> A 4GB take that fails at 3.9GB is missing, so
/// hash negotiation alone sends the whole 4GB again — on a domestic uplink that is hours, repeatedly, and it is
/// the single most likely reason a customer concludes sync does not work.
/// </para>
/// </summary>
public class WorkspaceUploadSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 14, 13, 0, 0, DateTimeKind.Utc);

    public WorkspaceUploadSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    /// <summary>Keeps objects by key so assembly and discard can both be observed.</summary>
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

            return Task.FromResult(new StagedBlob(
                tenantId, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length, key));
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

    private static WorkspaceUploadService CreateUploads(
        WorkspacesDbContext context, Guid tenantId, IFileStore store, WorkspaceOptions? options = null)
    {
        var blobs = new WorkspaceBlobService(
            context, store, new TestTenantProvider(tenantId), new TestClock(),
            NullLogger<WorkspaceBlobService>.Instance);

        return new WorkspaceUploadService(
            context, blobs, store, new TestTenantProvider(tenantId), new TestClock(),
            Microsoft.Extensions.Options.Options.Create(
                options ?? new WorkspaceOptions { PartSizeBytes = 8 }),
            NullLogger<WorkspaceUploadService>.Instance);
    }

    private static SubscriberRef Party(Guid id) => new(SubscriberKinds.Party, id);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string HashOf(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Stream Part(byte[] all, int partNumber, int partSize)
        => new MemoryStream(all.Skip((partNumber - 1) * partSize).Take(partSize).ToArray());

    // ── Resume ───────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnInterruptedUpload_Should_ResumeWithoutResendingWhatArrived()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var uploads = CreateUploads(context, tenantId, store);

        var content = Bytes("the undersong: a take that is long enough to need several parts");
        var hash = HashOf(content);

        var session = await uploads.BeginAsync(subscriber, hash, content.Length);
        session.MissingParts.Should().HaveCount(session.TotalParts);

        // Nine tenths of the way there, then the connection dies.
        foreach (var n in session.MissingParts.Take(session.TotalParts - 1))
        {
            await uploads.UploadPartAsync(session.Id(), n, Part(content, n, session.PartSizeBytes));
        }

        // The client comes back and asks the same question one level down: which parts do you not have.
        var resumed = await uploads.BeginAsync(subscriber, hash, content.Length);

        resumed.SessionId.Should().Be(session.SessionId, "resume is keyed on the hash, which a client can recompute");
        resumed.MissingParts.Should().ContainSingle()
            .Which.Should().Be(session.TotalParts, "only the tail is still needed");
    }

    [SkippableFact]
    public async Task ACompletedUpload_Should_AssembleToTheDeclaredHash()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var uploads = CreateUploads(context, tenantId, store);

        var content = Bytes("copper saints, act three, scene one");
        var hash = HashOf(content);

        var session = await uploads.BeginAsync(subscriber, hash, content.Length);

        foreach (var n in session.MissingParts)
        {
            await uploads.UploadPartAsync(session.Id(), n, Part(content, n, session.PartSizeBytes));
        }

        var result = await uploads.CompleteAsync(session.Id());

        result.ContentHash.Should().Be(hash);

        var contentKey = WorkspaceBlobService.ContentKeyFor(tenantId, hash);
        store.Objects.Should().ContainKey(contentKey);
        store.Objects[contentKey].Should().Equal(content, "the parts assemble in order");

        // Possession goes through the ordinary path, so a multipart upload is not a second place for
        // the §6 leak to reappear.
        (await context.Possessions.AsNoTracking().AnyAsync(p => p.ContentHash == hash))
            .Should().BeTrue();
    }

    [SkippableFact]
    public async Task ResendingAPart_Should_BeANoOp()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var uploads = CreateUploads(context, tenantId, new InMemoryFileStore());

        var content = Bytes("a take with several parts in it");
        var session = await uploads.BeginAsync(subscriber, HashOf(content), content.Length);

        await uploads.UploadPartAsync(session.Id(), 1, Part(content, 1, session.PartSizeBytes));
        await uploads.UploadPartAsync(session.Id(), 1, Part(content, 1, session.PartSizeBytes));

        // A client that lost its acknowledgement re-sends. Appending twice would corrupt the assembly
        // and the hash check would then reject a blob the client sent correctly.
        (await context.UploadParts.AsNoTracking().CountAsync(p => p.SessionId == session.SessionId))
            .Should().Be(1);
    }

    // ── The bounds ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task StreamingMoreThanDeclared_Should_BeAbortedAtTheBound()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var uploads = CreateUploads(
            context, tenantId, store, new WorkspaceOptions { PartSizeBytes = 1024 });

        var actual = Bytes(new string('x', 4096));

        // Declares far less than it intends to send. Without a bound the quota check that ran against
        // the declaration has been outrun by the transfer, and the bytes are on disk before anything
        // notices.
        var session = await uploads.BeginAsync(subscriber, HashOf(actual), 16);

        var act = async () => await uploads.UploadPartAsync(session.Id(), 1, new MemoryStream(actual));

        await act.Should().ThrowAsync<DeclaredLengthExceededException>();

        var after = await context.UploadSessions.AsNoTracking()
            .FirstAsync(s => s.Id == session.SessionId);

        after.Status.Should().Be(UploadSessionStatuses.Aborted);

        store.Objects.Keys.Should().NotContain(
            WorkspaceBlobService.ContentKeyFor(tenantId, HashOf(actual)),
            "no partial object is ever promoted");
    }

    [SkippableFact]
    public async Task PartsThatDoNotMatchTheDeclaredHash_Should_BeDiscarded()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var uploads = CreateUploads(context, tenantId, store);

        var claimed = Bytes("somebody elses content!");
        var sent = Bytes("what was really upload!");

        // Same length so the bound does not fire first — the hash check is what has to catch this.
        sent.Length.Should().Be(claimed.Length);

        var session = await uploads.BeginAsync(subscriber, HashOf(claimed), claimed.Length);

        foreach (var n in session.MissingParts)
        {
            await uploads.UploadPartAsync(session.Id(), n, Part(sent, n, session.PartSizeBytes));
        }

        var act = async () => await uploads.CompleteAsync(session.Id());

        // Verification on promote, not on trust. Without it a client could upload its own bytes under
        // someone else's hash and read that hash back as though it were theirs.
        await act.Should().ThrowAsync<UploadHashMismatchException>();

        store.Objects.Keys.Should().NotContain(WorkspaceBlobService.ContentKeyFor(tenantId, HashOf(claimed)));
        store.Objects.Keys.Should().NotContain(WorkspaceBlobService.ContentKeyFor(tenantId, HashOf(sent)));

        (await context.UploadSessions.AsNoTracking().FirstAsync(s => s.Id == session.SessionId))
            .Status.Should().Be(UploadSessionStatuses.Aborted);
    }

    [SkippableFact]
    public async Task CompletingWithAMissingPart_Should_Refuse()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var uploads = CreateUploads(context, tenantId, new InMemoryFileStore());

        var content = Bytes("a take with several parts in it");
        var session = await uploads.BeginAsync(subscriber, HashOf(content), content.Length);

        await uploads.UploadPartAsync(session.Id(), 1, Part(content, 1, session.PartSizeBytes));

        var act = async () => await uploads.CompleteAsync(session.Id());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Staging expiry ───────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnAbandonedSession_Should_BeSwept()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var uploads = CreateUploads(context, tenantId, store);

        var content = Bytes("an upload nobody finished");
        var session = await uploads.BeginAsync(subscriber, HashOf(content), content.Length);
        await uploads.UploadPartAsync(session.Id(), 1, Part(content, 1, session.PartSizeBytes));

        // Expire it the way time would.
        await context.UploadSessions
            .Where(s => s.Id == session.SessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, Now.AddHours(-1)));

        // ExecuteUpdate bypasses the tracker, so the entity this context still holds carries a stale
        // RowVersion and the sweeper's delete would fail on optimistic concurrency.
        context.ChangeTracker.Clear();

        var sweeper = new WorkspaceBlobSweeper(
            context, store, new TestTenantProvider(tenantId), new TestClock(),
            Microsoft.Extensions.Options.Options.Create(new WorkspaceOptions()),
            NullLogger<WorkspaceBlobSweeper>.Instance);

        var summary = await sweeper.SweepAsync();

        // This was a no-op until 091 gave staging a database record. Before that an abandoned
        // multi-gigabyte upload had no row at all, so it was invisible and paid for.
        summary.StagingRemoved.Should().Be(1);

        (await context.UploadSessions.AsNoTracking().AnyAsync(s => s.Id == session.SessionId))
            .Should().BeFalse();
        (await context.UploadParts.AsNoTracking().AnyAsync(p => p.SessionId == session.SessionId))
            .Should().BeFalse();
        store.Objects.Should().BeEmpty("the parts go with the session");
    }

    [SkippableFact]
    public async Task AnOpenUnexpiredSession_Should_NotBeSwept()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var store = new InMemoryFileStore();
        var uploads = CreateUploads(context, tenantId, store);

        var content = Bytes("an upload still in flight");
        var session = await uploads.BeginAsync(subscriber, HashOf(content), content.Length);
        await uploads.UploadPartAsync(session.Id(), 1, Part(content, 1, session.PartSizeBytes));

        var sweeper = new WorkspaceBlobSweeper(
            context, store, new TestTenantProvider(tenantId), new TestClock(),
            Microsoft.Extensions.Options.Options.Create(new WorkspaceOptions()),
            NullLogger<WorkspaceBlobSweeper>.Instance);

        // Sweeping a live upload would delete the first 90% of a take somebody is still sending.
        (await sweeper.SweepAsync()).StagingRemoved.Should().Be(0);

        (await context.UploadSessions.AsNoTracking().AnyAsync(s => s.Id == session.SessionId))
            .Should().BeTrue();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}

internal static class UploadSessionExtensions
{
    /// <summary>Reads better than <c>session.SessionId</c> at every call site.</summary>
    public static Guid Id(this UploadSession session) => session.SessionId;
}
