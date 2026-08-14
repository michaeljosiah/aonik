using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 089 P2 — the uniqueness and dedupe indexes, in the only lane that can prove them.
///
/// <para>
/// Acceptance criterion 3 names LocalDB explicitly, and the reason is not thoroughness. <strong>Filtered
/// unique indexes do not exist on the InMemory provider</strong>, so a workspace implementation that stored
/// identical bytes twice, or let two clients take the same sequence, would pass an InMemory suite completely.
/// The index is the mechanism; a test that cannot exercise the mechanism is asserting an intention.
/// </para>
/// </summary>
public class WorkspacesSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc);

    public WorkspacesSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private WorkspacesDbContext CreateContext(Guid tenantId)
        => new(
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseSqlServer(_db.ConnectionString)
                .Options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static Workspace AWorkspace(Guid tenantId, string slug = "the-undersong")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = WorkspaceKinds.World,
            Name = "The Undersong",
            Slug = slug,
            OwnerPartyId = Guid.NewGuid(),
            Status = WorkspaceStatuses.Active,
        };

    private static WorkspaceBlob ABlob(Guid tenantId, string hash, string key)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentHash = hash,
            StorageKey = key,
            SizeBytes = 1024,
            RefCount = 1,
        };

    private static WorkspaceRevision ARevision(
        Guid tenantId, Guid workspaceId, long sequence, Guid commitId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            Sequence = sequence,
            CommitId = commitId,
            RequestHash = new string('a', 64),
            AuthorPartyId = Guid.NewGuid(),
            State = RevisionStates.FastForward,
            CommittedAt = Now,
        };

    // ── The dedupe ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task IdenticalBytes_Should_OccupyOneBlob()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var hash = new string('b', 64);
        await using var context = CreateContext(tenantId);

        context.Blobs.Add(ABlob(tenantId, hash, "workspaces/x/blobs/bb/" + hash));
        await context.SaveChangesAsync();

        // The same content arriving a second time — a re-sync, a second machine, an unchanged sheet in
        // a new revision. One physical object, not one row over two objects.
        context.Blobs.Add(ABlob(tenantId, hash, "workspaces/x/blobs/bb/" + hash + "-duplicate"));

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "content addressing is only dedupe if the database refuses the second row");
    }

    [SkippableFact]
    public async Task TheSameHash_Should_BeStoredOncePerTenant_NotOncePerPlatform()
    {
        RequireSqlServer();

        var hash = new string('c', 64);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await using (var context = CreateContext(first))
        {
            context.Blobs.Add(ABlob(first, hash, "workspaces/1/blobs/cc/" + hash));
            await context.SaveChangesAsync();
        }

        await using var other = CreateContext(second);
        other.Blobs.Add(ABlob(second, hash, "workspaces/2/blobs/cc/" + hash));

        // Deliberately allowed. Deduping across tenants would mean one tenant's deletion could remove
        // bytes another tenant still names, and would let a tenant learn that content exists elsewhere
        // by observing an upload it did not have to make.
        var act = async () => await other.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    // ── Idempotency and ordering are different indexes ───────────────────

    [SkippableFact]
    public async Task TheSameCommitId_Should_NotBeStoredTwiceForOneWorkspace()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = AWorkspace(tenantId, $"ws-{Guid.NewGuid():N}");
        context.Workspaces.Add(workspace);
        var commitId = Guid.NewGuid();
        context.Revisions.Add(ARevision(tenantId, workspace.Id, 1, commitId));
        await context.SaveChangesAsync();

        context.Revisions.Add(ARevision(tenantId, workspace.Id, 2, commitId));

        // Idempotency lives here and nowhere else. A retried commit lands on the CommitId; the sequence
        // is the server's and is never what a client compares against.
        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task TheSameSequence_Should_NotBeStoredTwiceForOneWorkspace()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = AWorkspace(tenantId, $"ws-{Guid.NewGuid():N}");
        context.Workspaces.Add(workspace);
        context.Revisions.Add(ARevision(tenantId, workspace.Id, 1, Guid.NewGuid()));
        await context.SaveChangesAsync();

        context.Revisions.Add(ARevision(tenantId, workspace.Id, 1, Guid.NewGuid()));

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "history has one revision per position, whatever the divergence state");
    }

    [SkippableFact]
    public async Task ADivergentRevision_Should_StillTakeASequence()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var workspace = AWorkspace(tenantId, $"ws-{Guid.NewGuid():N}");
        context.Workspaces.Add(workspace);

        var head = ARevision(tenantId, workspace.Id, 1, Guid.NewGuid());
        var diverged = ARevision(tenantId, workspace.Id, 2, Guid.NewGuid());
        diverged.State = RevisionStates.Diverged;
        diverged.ParentRevisionId = null;

        context.Revisions.AddRange(head, diverged);

        // It is stored and sequenced; it simply does not advance the head. Rejecting it at the database
        // would make the conflict flow unreachable and show the user a uniqueness error instead of a
        // proposal — the exact failure §6.1 corrected.
        var act = async () => await context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    // ── One path per revision ────────────────────────────────────────────

    [SkippableFact]
    public async Task OnePath_Should_AppearOncePerRevision()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var revisionId = Guid.NewGuid();

        WorkspaceFile AFile() => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RevisionId = revisionId,
            Path = "scenes/act-one/opening.md",
            ContentHash = new string('d', 64),
            SizeBytes = 42,
        };

        context.Files.Add(AFile());
        await context.SaveChangesAsync();

        context.Files.Add(AFile());

        // The manifest IS the revision, so two rows for one path is not a duplicate record — it is an
        // ambiguous tree, and a checkout would have to pick one.
        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task ASlug_Should_BeUniquePerTenant()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var slug = $"ws-{Guid.NewGuid():N}";
        await using var context = CreateContext(tenantId);

        context.Workspaces.Add(AWorkspace(tenantId, slug));
        await context.SaveChangesAsync();

        context.Workspaces.Add(AWorkspace(tenantId, slug));

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task ThreeGigabytes_Should_BeCountedWithoutTruncation()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);

        var workspace = AWorkspace(tenantId, $"ws-{Guid.NewGuid():N}");
        workspace.TotalBytes = 3L * 1024 * 1024 * 1024;
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();

        // int overflows just past 2GB, and a world of takes passes that on day one. The failure would
        // be a negative quota reading rather than an error.
        var stored = await context.Workspaces.AsNoTracking().FirstAsync(w => w.Id == workspace.Id);
        stored.TotalBytes.Should().Be(3L * 1024 * 1024 * 1024);
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
