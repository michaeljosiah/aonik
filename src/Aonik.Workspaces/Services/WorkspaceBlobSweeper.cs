using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Persistence;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Removes unreferenced blobs and abandoned staging objects (Spec 089 §5.1).
///
/// <para>
/// <strong>A reference count reaching zero is not a stable fact.</strong> The sweeper reads <c>RefCount = 0</c>
/// and selects the blob; before it deletes the object a new commit legitimately references that hash — the row
/// still exists, so validation passes and the count increments. Deleting now acts on an observation that is
/// already stale, and the freshly committed revision points at bytes that are gone.
/// </para>
///
/// <para>
/// The symptom is the worst kind: the commit succeeded, the manifest is valid, the database is consistent, and
/// the file is simply missing — found much later by a user opening a revision. Deleting <em>lazily</em> does not
/// help, because the window sits between the read and the delete wherever that is in time.
/// </para>
///
/// <para>
/// So deletion <strong>claims</strong> the blob before touching storage, and referencing refuses a claimed blob.
/// Not a lock, which would serialise every commit in a tenant against a background job for a race this rare, and
/// not a longer delay, which only moves the window: the claim makes the two operations mutually exclusive by
/// <em>data</em>. Whichever writes first wins, and the loser's safe branch is "upload it again" — one redundant
/// transfer, and never a dangling reference.
/// </para>
/// </summary>
public interface IWorkspaceBlobSweeper
{
    Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(CancellationToken cancellationToken = default);

    Task<BlobSweepSummary> SweepAsync(CancellationToken cancellationToken = default);
}

/// <param name="Abandoned">
/// Claimed, then found referenced again before the delete. Not a failure — it is the mechanism working.
/// </param>
/// <param name="StagingRemoved">Abandoned upload sessions reclaimed. No longer always zero (Spec 091 §7).</param>
public sealed record BlobSweepSummary(int Deleted, int Abandoned, int StagingRemoved);

internal sealed class WorkspaceBlobSweeper : IWorkspaceBlobSweeper
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IFileStore _fileStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<WorkspaceBlobSweeper> _logger;

    public WorkspaceBlobSweeper(
        IWorkspaceDataContext dbContext,
        IFileStore fileStore,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<WorkspaceOptions> options,
        ILogger<WorkspaceBlobSweeper> logger)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(
        CancellationToken cancellationToken = default)
        => await _dbContext.Blobs
            .AsNoTracking().AcrossTenants()
            .Where(b => !b.IsDeleted && b.RefCount == 0)
            .Select(b => b.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<BlobSweepSummary> SweepAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        // Unreferenced for long enough to be sure it is not an upload waiting for its commit. Storing
        // is not referencing (§5), so a blob uploaded seconds ago legitimately sits at zero.
        var cutoff = now.AddHours(-_options.UnreferencedGraceHours);

        var candidates = await _dbContext.Blobs
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && b.RefCount == 0
                && !b.IsDeleting
                && b.CreatedAt <= cutoff)
            .Select(b => new { b.Id, b.ContentHash, b.StorageKey })
            .Take(_options.SweepBatchSize)
            .ToListAsync(cancellationToken);

        var deleted = 0;
        var abandoned = 0;

        foreach (var candidate in candidates)
        {
            // THE CLAIM. One statement, guarded on still-unreferenced and not-already-claimed. Zero
            // rows means somebody referenced it or another sweeper got there first — either way this
            // pass has no right to the bytes.
            var claimed = await _dbContext.Blobs
                .Where(b => b.TenantId == tenantId
                    && b.Id == candidate.Id
                    && b.RefCount == 0
                    && !b.IsDeleting)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(b => b.IsDeleting, true)
                          .SetProperty(b => b.DeletingSince, now),
                    cancellationToken);

            if (claimed == 0)
            {
                abandoned++;
                continue;
            }

            // Re-check UNDER the claim. A reference could have landed between selecting the candidate
            // and claiming it; the claim would still have succeeded because RefCount was read stale.
            var referencedNow = await _dbContext.Blobs
                .AsNoTracking()
                .AnyAsync(b => b.TenantId == tenantId && b.Id == candidate.Id && b.RefCount > 0,
                    cancellationToken);

            if (referencedNow)
            {
                await ReleaseClaimAsync(tenantId, candidate.Id, cancellationToken);
                abandoned++;
                continue;
            }

            try
            {
                await _fileStore.DeleteAsync(candidate.StorageKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The row stays claimed and the object stays put. Clearing the claim would offer these
                // bytes back to a referencing client while storage still holds them under a key we
                // just failed to remove — worse than leaving an object to the next pass.
                _logger.LogError(ex,
                    "Could not delete workspace blob object {StorageKey}; leaving the claim in place.",
                    candidate.StorageKey);

                continue;
            }

            await _dbContext.Blobs
                .Where(b => b.TenantId == tenantId && b.Id == candidate.Id)
                .ExecuteDeleteAsync(cancellationToken);

            deleted++;
        }

        var staging = await SweepStagingAsync(tenantId, now, cancellationToken);

        return new BlobSweepSummary(deleted, abandoned, staging);
    }

    private Task ReleaseClaimAsync(Guid tenantId, Guid blobId, CancellationToken cancellationToken)
        => _dbContext.Blobs
            .Where(b => b.TenantId == tenantId && b.Id == blobId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.IsDeleting, false)
                      .SetProperty(b => b.DeletingSince, (DateTime?)null),
                cancellationToken);

    /// <summary>
    /// Staged objects whose upload never finished.
    ///
    /// <para>
    /// They have no row — staging is a storage concern, not a database one — so nothing else would ever
    /// find them. An abandoned multi-gigabyte upload is invisible and billed.
    /// </para>
    /// </summary>
    /// <summary>
    /// Removes abandoned upload sessions and their parts (Spec 091 §7).
    ///
    /// <para>
    /// This was a no-op until 091 gave staging a database record. Before that an abandoned multi-gigabyte upload
    /// had no row at all — nothing could find it without enumerating a storage prefix, which <c>IFileStore</c>
    /// cannot do — so it was invisible and paid for. The session row is what makes it sweepable.
    /// </para>
    /// </summary>
    private async Task<int> SweepStagingAsync(
        Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var stale = await _dbContext.UploadSessions
            .Include(s => s.Parts)
            .Where(s => s.TenantId == tenantId
                && (s.Status != Entities.UploadSessionStatuses.Open || s.ExpiresAt <= now))
            .Take(_options.SweepBatchSize)
            .ToListAsync(cancellationToken);

        var removed = 0;

        foreach (var session in stale)
        {
            var allDeleted = true;

            foreach (var part in session.Parts)
            {
                try
                {
                    await _fileStore.DeleteAsync(part.StorageKey, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The row stays so the next pass tries again. Dropping it would strand the object
                    // exactly as the pre-091 path did.
                    _logger.LogWarning(ex,
                        "Could not delete upload part {StorageKey}; leaving the session for the next pass.",
                        part.StorageKey);

                    allDeleted = false;
                    break;
                }
            }

            if (!allDeleted)
            {
                continue;
            }

            _dbContext.UploadParts.RemoveRange(session.Parts);
            _dbContext.UploadSessions.Remove(session);
            removed++;
        }

        if (removed > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return removed;
    }
}

public sealed class WorkspaceOptions
{
    public const string SectionName = "Workspaces";

    /// <summary>
    /// How long a blob may sit unreferenced before it is sweepable.
    ///
    /// <para>
    /// Not zero, because storing is not referencing: a client uploads blobs and <em>then</em> commits the
    /// manifest that names them, so every upload passes through a legitimate window at <c>RefCount = 0</c>.
    /// Sweeping eagerly would delete content out from under a commit in progress.
    /// </para>
    /// </summary>
    public int UnreferencedGraceHours { get; set; } = 24;

    public int SweepBatchSize { get; set; } = 500;

    /// <summary>
    /// Above this, an upload goes through the multipart path (Spec 091 §7).
    ///
    /// <para>
    /// 32MB. Below it the part bookkeeping costs more than the retry it saves; above it a failed transfer is
    /// painful on a domestic uplink.
    /// </para>
    /// </summary>
    public long MultipartThresholdBytes { get; set; } = 32L * 1024 * 1024;

    public int PartSizeBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>
    /// How long an open upload session survives without progress.
    ///
    /// <para>
    /// Long enough that a client resuming the next morning still has its parts, and finite because incomplete
    /// uploads must not be billable and must not accumulate.
    /// </para>
    /// </summary>
    public int UploadSessionHours { get; set; } = 48;
}
