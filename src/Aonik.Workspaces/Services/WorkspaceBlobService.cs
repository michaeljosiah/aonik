using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
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
    Task<BlobStoreResult> StoreAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these hashes the tenant does <strong>not</strong> hold.
    ///
    /// <para>
    /// A blob under a deletion claim counts as missing. Reporting it as present would let a client skip the
    /// upload and then commit a manifest naming bytes that are being removed.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> FindMissingAsync(
        IReadOnlyList<string> contentHashes, CancellationToken cancellationToken = default);

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
        Stream content, CancellationToken cancellationToken = default)
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

            return new BlobStoreResult(staged.ContentHash, staged.SizeBytes, AlreadyPresent: true);
        }

        return new BlobStoreResult(
            staged.ContentHash,
            staged.SizeBytes,
            promoted.Outcome == PromoteOutcome.AlreadyPresent);
    }

    public async Task<IReadOnlyList<string>> FindMissingAsync(
        IReadOnlyList<string> contentHashes, CancellationToken cancellationToken = default)
    {
        if (contentHashes.Count == 0)
        {
            return [];
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var held = await _dbContext.Blobs
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && contentHashes.Contains(b.ContentHash)
                // A claimed blob is missing as far as any caller is concerned.
                && !b.IsDeleting)
            .Select(b => b.ContentHash)
            .ToListAsync(cancellationToken);

        var heldSet = held.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. contentHashes.Where(h => !heldSet.Contains(h)).Distinct(StringComparer.OrdinalIgnoreCase)];
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
