using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Content-addressed storage for workspace files (Spec 089 §5).
/// </summary>
public interface IWorkspaceBlobService
{
    /// <summary>
    /// Store bytes, or recognise that the tenant already has them.
    ///
    /// <para>
    /// Staging happens before the database is consulted, because the hash is not known until the bytes have
    /// streamed past — and the hash is the identity. A duplicate therefore costs one transfer and no second
    /// object.
    /// </para>
    /// </summary>
    Task<BlobStoreResult> StoreAsync(
        SubscriberRef subscriber, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these hashes the <strong>caller</strong> does not possess and cannot reach (Spec 091 &sect;6).
    ///
    /// <para>
    /// Relative to the caller, never to physical storage. Answering from blob existence breaks in both
    /// directions for a hash that exists but belongs to another subscriber: say "present" and it leaks the
    /// existence oracle 089 &sect;12 closed <em>and</em> the client skips the upload, so commit then refuses the
    /// hash as unpossessed and the client is deadlocked. Negotiation and commit have to answer the same
    /// question, which is what makes the protocol terminate.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> FindMissingAsync(
        SubscriberRef subscriber,
        Guid callerPartyId,
        IReadOnlyList<string> contentHashes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim a reference for each hash, or report which could not be claimed.
    ///
    /// <para>
    /// The increment is guarded on the blob not being under a deletion claim <em>in the same statement</em>, so a
    /// claim that lands between a caller's read and its write loses. A hash that cannot be referenced is returned
    /// as missing and the caller re-uploads — one redundant transfer in a rare case, and never a manifest
    /// pointing at bytes that are gone.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> AddReferencesAsync(
        IReadOnlyList<string> contentHashes, CancellationToken cancellationToken = default);

    /// <summary>Release references when a revision is deleted or rejected. Never deletes inline.</summary>
    Task ReleaseReferencesAsync(
        IReadOnlyList<string> contentHashes, CancellationToken cancellationToken = default);
}

/// <param name="AlreadyPresent">True when the tenant already held these exact bytes.</param>
public sealed record BlobStoreResult(string ContentHash, long SizeBytes, bool AlreadyPresent);

internal sealed class WorkspaceBlobService : IWorkspaceBlobService
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IFileStore _fileStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<WorkspaceBlobService> _logger;

    public WorkspaceBlobService(
        IWorkspaceDataContext dbContext,
        IFileStore fileStore,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<WorkspaceBlobService> logger)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <summary><c>workspaces/{tenantId:N}/blobs/{hash[0..2]}/{hash}</c> (§5).</summary>
    public static string ContentKeyFor(Guid tenantId, string contentHash)
        => $"workspaces/{tenantId:N}/blobs/{contentHash[..2]}/{contentHash}";

    public async Task<BlobStoreResult> StoreAsync(
        SubscriberRef subscriber, Stream content, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var staged = await _fileStore.StageAsync(tenantId, content, cancellationToken);
        var contentKey = ContentKeyFor(tenantId, staged.ContentHash);

        var promoted = await _fileStore.PromoteAsync(staged, contentKey, cancellationToken);

        var existing = await _dbContext.Blobs
            .FirstOrDefaultAsync(
                b => b.TenantId == tenantId && b.ContentHash == staged.ContentHash, cancellationToken);

        if (existing is not null)
        {
            if (existing.IsDeleting)
            {
                // The object is back — this upload just wrote it. Standing the row back up is correct
                // and is the sweeper's abandon condition: it re-checks under its own claim and finds
                // the world changed.
                existing.IsDeleting = false;
                existing.DeletingSince = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Possession is established EVEN THOUGH the object already existed. The caller supplied the
            // bytes, so they have demonstrated possession and the physical object is simply reused. This
            // is the path that closes the negotiation loop, and the one an implementation is most likely
            // to "optimise" back into a leak.
            await RecordPossessionAsync(
                tenantId, subscriber, staged.ContentHash, existing.SizeBytes, cancellationToken);

            return new BlobStoreResult(staged.ContentHash, existing.SizeBytes, AlreadyPresent: true);
        }

        _dbContext.Blobs.Add(new WorkspaceBlob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentHash = staged.ContentHash,
            StorageKey = contentKey,
            SizeBytes = staged.SizeBytes,
            // Zero until a manifest names it. Storing is not referencing: an upload that is never
            // committed must become sweepable rather than pinned forever.
            RefCount = 0,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index did its job: a concurrent upload of identical bytes inserted first.
            // Both wrote the same object under the same key, so there is nothing to clean up.
            _logger.LogDebug(
                "Blob {ContentHash} was inserted concurrently; treating as already present.",
                staged.ContentHash);

            await RecordPossessionAsync(
                tenantId, subscriber, staged.ContentHash, staged.SizeBytes, cancellationToken);

            return new BlobStoreResult(staged.ContentHash, staged.SizeBytes, AlreadyPresent: true);
        }

        await RecordPossessionAsync(
            tenantId, subscriber, staged.ContentHash, staged.SizeBytes, cancellationToken);

        return new BlobStoreResult(
            staged.ContentHash,
            staged.SizeBytes,
            promoted.Outcome == PromoteOutcome.AlreadyPresent);
    }

    public async Task<IReadOnlyList<string>> FindMissingAsync(
        SubscriberRef subscriber,
        Guid callerPartyId,
        IReadOnlyList<string> contentHashes,
        CancellationToken cancellationToken = default)
    {
        if (contentHashes.Count == 0)
        {
            return [];
        }

        var reachable = await ReachableHashesAsync(
            subscriber, callerPartyId, contentHashes, cancellationToken);

        return
        [
            .. contentHashes
                .Where(h => !reachable.Contains(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// The two routes to possession (089 &sect;12): uploaded by this subscriber, or already reachable through a
    /// workspace this caller can read.
    ///
    /// <para>
    /// <strong>Tenant scope alone is not sufficient</strong>, and that is the finding this replaces. Accepting
    /// any hash with a blob in the tenant is fine when a tenant is one customer and wrong for Arke Kids, where
    /// one tenant holds many unrelated families. Guessing is not required either — hashes of shared or
    /// previously-seen content are knowable, and a match turns into a read. A content hash is a <em>name</em>;
    /// treating it as an <em>authorisation</em> is what makes it a bearer token.
    /// </para>
    /// </summary>
    private async Task<HashSet<string>> ReachableHashesAsync(
        SubscriberRef subscriber,
        Guid callerPartyId,
        IReadOnlyList<string> contentHashes,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Route one: they supplied the bytes.
        var possessed = await _dbContext.Possessions
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && p.SubscriberKind == subscriber.Kind
                && p.SubscriberId == subscriber.Id
                && contentHashes.Contains(p.ContentHash))
            .Select(p => p.ContentHash)
            .ToListAsync(cancellationToken);

        var reachable = possessed.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Route two: it is already in a workspace they can read.
        var readableWorkspaceIds = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                && w.OwnerPartyId == callerPartyId
                && w.Status == WorkspaceStatuses.Active)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        if (readableWorkspaceIds.Count > 0)
        {
            var revisionIds = await _dbContext.Revisions
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId && readableWorkspaceIds.Contains(r.WorkspaceId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var named = await _dbContext.Files
                .AsNoTracking()
                .Where(f => f.TenantId == tenantId
                    && revisionIds.Contains(f.RevisionId)
                    && contentHashes.Contains(f.ContentHash))
                .Select(f => f.ContentHash)
                .ToListAsync(cancellationToken);

            reachable.UnionWith(named);
        }

        if (reachable.Count == 0)
        {
            return reachable;
        }

        // Whatever route got here, a blob the sweeper has claimed is missing as far as any caller is
        // concerned: reporting it present lets a client skip the upload and commit a manifest naming
        // bytes that are being removed.
        var claimedHashes = reachable.ToList();

        var claimed = await _dbContext.Blobs
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsDeleting && claimedHashes.Contains(b.ContentHash))
            .Select(b => b.ContentHash)
            .ToListAsync(cancellationToken);

        reachable.ExceptWith(claimed);

        return reachable;
    }

    /// <summary>
    /// Records that a subscriber supplied these bytes.
    ///
    /// <para>
    /// <c>WorkspaceCount</c> starts at zero: possession is proof they supplied the bytes, which is a different
    /// fact from a workspace referencing them. The count rises when a manifest names it, and the ceiling claim
    /// rides with that — an upload nobody commits should not be billed forever.
    /// </para>
    /// </summary>
    private async Task RecordPossessionAsync(
        Guid tenantId,
        SubscriberRef subscriber,
        string contentHash,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        var already = await _dbContext.Possessions
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId
                && p.SubscriberKind == subscriber.Kind
                && p.SubscriberId == subscriber.Id
                && p.ContentHash == contentHash, cancellationToken);

        if (already)
        {
            return;
        }

        _dbContext.Possessions.Add(new BlobPossession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = subscriber.Kind,
            SubscriberId = subscriber.Id,
            ContentHash = contentHash,
            SizeBytes = sizeBytes,
            WorkspaceCount = 0,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> AddReferencesAsync(
        IReadOnlyList<string> contentHashes, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var unclaimable = new List<string>();

        foreach (var group in contentHashes.GroupBy(h => h, StringComparer.OrdinalIgnoreCase))
        {
            var hash = group.Key;
            var references = group.Count();

            // ONE statement: read, guard and increment together. Splitting it would reopen exactly the
            // race the tombstone exists to close — a claim landing between the read and the write.
            var updated = await _dbContext.Blobs
                .Where(b => b.TenantId == tenantId && b.ContentHash == hash && !b.IsDeleting)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(b => b.RefCount, b => b.RefCount + references),
                    cancellationToken);

            if (updated == 0)
            {
                unclaimable.Add(hash);
            }
        }

        return unclaimable;
    }

    public async Task ReleaseReferencesAsync(
        IReadOnlyList<string> contentHashes, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        foreach (var group in contentHashes.GroupBy(h => h, StringComparer.OrdinalIgnoreCase))
        {
            var hash = group.Key;
            var references = group.Count();

            // Floored at zero rather than allowed to go negative. A negative count would make a blob
            // permanently unsweepable, and the bug would be invisible until a storage bill arrived.
            await _dbContext.Blobs
                .Where(b => b.TenantId == tenantId && b.ContentHash == hash)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(
                        b => b.RefCount,
                        b => b.RefCount > references ? b.RefCount - references : 0),
                    cancellationToken);
        }
    }
}
