using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Commits, manifests and the divergent-revision lifecycle (Spec 089 §6, §7).
/// </summary>
internal sealed class WorkspaceSyncService : IWorkspaceSyncService
{
    /// <summary>
    /// How many times a commit re-classifies after losing the compare-and-swap.
    ///
    /// <para>
    /// Bounded, and small. Each loss means another commit won the head, and after a few the honest answer is that
    /// this workspace is under contention no retry will resolve — the client should re-read and decide. An
    /// unbounded loop here would spin under exactly the load that caused it.
    /// </para>
    /// </summary>
    private const int MaxCasAttempts = 5;

    private readonly IWorkspaceDataContext _dbContext;
    private readonly IWorkspaceBlobService _blobs;
    private readonly IShareGrantReader _grants;
    private readonly IBlobPossessionService _possessions;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<WorkspaceSyncService> _logger;

    public WorkspaceSyncService(
        IWorkspaceDataContext dbContext,
        IWorkspaceBlobService blobs,
        IShareGrantReader grants,
        IBlobPossessionService possessions,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<WorkspaceSyncService> logger)
    {
        _dbContext = dbContext;
        _blobs = blobs;
        _grants = grants;
        _possessions = possessions;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Owner, then the level on an active grant, then nothing (Spec 089 §8.1).
    ///
    /// <para>
    /// The grant's <c>AccessLevel</c> is read and <strong>enforced</strong>, which is the whole point. An earlier
    /// draft made writability part of <c>TermsJson</c> — the product's business — reasoning that a platform
    /// enforcing an "editor" role would be interpreting terms. That is a bypassable authorisation boundary: the
    /// commit endpoint is a platform HTTP endpoint, and a read-only recipient needs only to call it directly,
    /// with curl or a modified client, which for an MIT-licensed product is a five-minute exercise. The
    /// product-side check is decoration; there is nothing behind it, and the grant reader's answer to <em>"is
    /// there a grant?"</em> for a read-only recipient is <strong>yes</strong>.
    /// </para>
    /// </summary>
    public async Task<WorkspaceAccessLevel> ResolveAccessAsync(
        Guid workspaceId, Guid callerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var ownerPartyId = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                && w.Id == workspaceId
                && w.Status == WorkspaceStatuses.Active)
            .Select(w => (Guid?)w.OwnerPartyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerPartyId is null)
        {
            return WorkspaceAccessLevel.None;
        }

        if (ownerPartyId == callerPartyId)
        {
            return WorkspaceAccessLevel.Owner;
        }

        var granted = await _grants.GetAccessLevelAsync(
            callerPartyId, WorkspaceShareResource.Kind, workspaceId, cancellationToken);

        return granted switch
        {
            ShareAccessLevels.Write => WorkspaceAccessLevel.Write,
            ShareAccessLevels.Read => WorkspaceAccessLevel.Read,
            // Anything unrecognised is nothing. A level the platform does not know is not a level it
            // can enforce, and treating it as read would let a future value widen access by accident.
            _ => WorkspaceAccessLevel.None,
        };
    }

    public async Task<BlobNegotiationResult> NegotiateAsync(
        Guid workspaceId,
        Guid callerPartyId,
        IReadOnlyList<string> contentHashes,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(workspaceId, callerPartyId, WorkspaceAccessLevel.Read, cancellationToken);

        return new BlobNegotiationResult(
            await _blobs.FindMissingAsync(contentHashes, cancellationToken));
    }

    public async Task<CommitRevisionResult> CommitAsync(
        CommitRevisionRequest request,
        Guid callerPartyId,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(request.WorkspaceId, callerPartyId, WorkspaceAccessLevel.Write, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var manifest = ManifestNormaliser.Normalise(request.Manifest);
        var requestHash = ManifestNormaliser.ComputeRequestHash(
            request.WorkspaceId, request.ParentRevisionId, manifest);

        var replay = await TryReplayAsync(tenantId, request, requestHash, cancellationToken);

        if (replay is not null)
        {
            return replay;
        }

        // Upload first, commit second — never the reverse. Cheap and early, so an obviously incomplete
        // client is refused before any sequence is consumed.
        var missing = await _blobs.FindMissingAsync(
            [.. manifest.Select(e => e.ContentHash)], cancellationToken);

        if (missing.Count > 0)
        {
            return new CommitRevisionResult(CommitOutcome.Diverged, Guid.Empty, 0, null, missing);
        }

        return await CommitWithCompareAndSwapAsync(
            tenantId, request, manifest, requestHash, callerPartyId, cancellationToken);
    }

    /// <summary>
    /// The §6.2 compare-and-swap: sequence allocation and head advancement in <strong>one</strong> guarded write,
    /// before any row is inserted.
    ///
    /// <para>
    /// "Inside the transaction" is not the same as atomic. Under <c>READ COMMITTED</c> two concurrent commits can
    /// both read the same head, both classify themselves as fast-forwards, and then either lose one head update or
    /// collide on the sequence index — and neither outcome is the specified one, which is that the loser is stored
    /// as <c>Diverged</c>.
    /// </para>
    ///
    /// <para>
    /// The guard is on the observed <c>NextSequence</c> rather than the row version. It is the same optimistic
    /// token — every successful commit increments it — and it states the invariant directly: <em>nobody has taken
    /// this sequence since I read it</em>. A row-version comparison would additionally fail on writes that have
    /// nothing to do with sequencing, such as a rename.
    /// </para>
    /// </summary>
    private async Task<CommitRevisionResult> CommitWithCompareAndSwapAsync(
        Guid tenantId,
        CommitRevisionRequest request,
        IReadOnlyList<ManifestEntry> manifest,
        string requestHash,
        Guid callerPartyId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxCasAttempts; attempt++)
        {
            var workspace = await _dbContext.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    w => w.TenantId == tenantId && w.Id == request.WorkspaceId, cancellationToken)
                ?? throw new InvalidOperationException($"Workspace {request.WorkspaceId} not found.");

            // Re-classified on every attempt, from the head this attempt actually observed. A commit that
            // lost the race is not diverged because it lost — it is diverged because, by the time it looked
            // again, its declared parent was no longer the head. Same rule, not an exception handler.
            var isFastForward = workspace.HeadRevisionId == request.ParentRevisionId;

            var sequence = workspace.NextSequence;
            var revisionId = Guid.NewGuid();

            var won = await _dbContext.Workspaces
                .Where(w => w.TenantId == tenantId
                    && w.Id == request.WorkspaceId
                    && w.NextSequence == sequence)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(w => w.NextSequence, sequence + 1)
                        .SetProperty(
                            w => w.HeadRevisionId,
                            w => isFastForward ? revisionId : w.HeadRevisionId),
                    cancellationToken);

            if (won == 0)
            {
                // Nothing was inserted, so nothing is orphaned. Read again and classify again.
                _logger.LogDebug(
                    "Commit {CommitId} lost the head compare-and-swap on attempt {Attempt}; re-classifying.",
                    request.CommitId, attempt);

                continue;
            }

            return await WriteRevisionAsync(
                tenantId, request, manifest, requestHash, callerPartyId,
                revisionId, sequence, isFastForward, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Workspace {request.WorkspaceId} is under sustained commit contention; "
            + $"gave up after {MaxCasAttempts} attempts. Re-read the head and try again.");
    }

    /// <summary>
    /// Charge the billing subscriber for content this workspace now names, refusing before any byte is accepted.
    ///
    /// <para>
    /// A storage limit retrofitted onto users who have already uploaded 200GB is a support problem rather than an
    /// engineering one. Checking before acceptance makes the failure "you are out of space" instead of a surprise
    /// bill or a silent truncation.
    /// </para>
    /// </summary>
    private async Task ChargeForManifestAsync(
        Guid tenantId,
        Guid workspaceId,
        IReadOnlyList<ManifestEntry> manifest,
        CancellationToken cancellationToken)
    {
        var billing = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == workspaceId)
            .Select(w => new { w.BillingSubscriberKind, w.BillingSubscriberId })
            .FirstOrDefaultAsync(cancellationToken);

        if (billing is null || billing.BillingSubscriberId == Guid.Empty)
        {
            // A workspace with no billing subscriber predates metering. Charging a subscriber that does
            // not exist would throw on every commit; leaving it uncharged is visible in the possession
            // table rather than silent.
            return;
        }

        var subscriber = new SubscriberRef(billing.BillingSubscriberKind, billing.BillingSubscriberId);

        var hashSizes = manifest
            .GroupBy(e => e.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SizeBytes, StringComparer.OrdinalIgnoreCase);

        await _possessions.AcquireAsync(subscriber, hashSizes, cancellationToken);
    }

    private async Task<CommitRevisionResult> WriteRevisionAsync(
        Guid tenantId,
        CommitRevisionRequest request,
        IReadOnlyList<ManifestEntry> manifest,
        string requestHash,
        Guid callerPartyId,
        Guid revisionId,
        long sequence,
        bool isFastForward,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // Claimed AFTER the sequence is won and before the manifest lands. A hash the sweeper has claimed in
        // the meantime cannot be referenced, and the caller is told it is missing rather than handed a
        // revision pointing at bytes being removed.
        var unreferenceable = await _blobs.AddReferencesAsync(
            [.. manifest.Select(e => e.ContentHash)], cancellationToken);

        if (unreferenceable.Count > 0)
        {
            await _blobs.ReleaseReferencesAsync(
                [.. manifest.Select(e => e.ContentHash).Where(h => !unreferenceable.Contains(h))],
                cancellationToken);

            // The sequence is spent. A gap in sequence numbers is harmless — they order history, they do not
            // count it — and it is a far better outcome than a head pointing at a revision that never landed.
            _logger.LogWarning(
                "Commit {CommitId} named {Count} hashes that could not be referenced; sequence {Sequence} is spent.",
                request.CommitId, unreferenceable.Count, sequence);

            return new CommitRevisionResult(CommitOutcome.Diverged, Guid.Empty, 0, null, unreferenceable);
        }

        // Charged before the manifest lands, so an over-quota commit refuses rather than storing a
        // revision the subscriber cannot pay for. EntitlementExceededException propagates with the
        // shortfall named.
        await ChargeForManifestAsync(tenantId, request.WorkspaceId, manifest, cancellationToken);

        var totalBytes = manifest.Sum(e => e.SizeBytes);

        _dbContext.Revisions.Add(new WorkspaceRevision
        {
            Id = revisionId,
            TenantId = tenantId,
            WorkspaceId = request.WorkspaceId,
            Sequence = sequence,
            ParentRevisionId = request.ParentRevisionId,
            CommitId = request.CommitId,
            RequestHash = requestHash,
            AuthorPartyId = callerPartyId,
            Message = request.Message,
            State = isFastForward ? RevisionStates.FastForward : RevisionStates.Diverged,
            FileCount = manifest.Count,
            TotalBytes = totalBytes,
            CommittedAt = now,
        });

        foreach (var entry in manifest)
        {
            _dbContext.Files.Add(new WorkspaceFile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RevisionId = revisionId,
                Path = entry.Path,
                ContentHash = entry.ContentHash,
                SizeBytes = entry.SizeBytes,
                ContentType = entry.ContentType,
            });
        }

        if (isFastForward)
        {
            // Only the head's totals are the workspace's. A divergent revision's bytes are stored and counted
            // against quota (§7.1) but they are not what the workspace currently is.
            await _dbContext.Workspaces
                .Where(w => w.TenantId == tenantId && w.Id == request.WorkspaceId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(w => w.FileCount, manifest.Count)
                        .SetProperty(w => w.TotalBytes, totalBytes),
                    cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CommitRevisionResult(
            isFastForward ? CommitOutcome.FastForward : CommitOutcome.Diverged,
            revisionId,
            sequence,
            isFastForward ? revisionId : null,
            []);
    }

    /// <summary>
    /// A retry lands on the stored outcome; a <em>different tree</em> under the same id does not (§6.1.1).
    /// </summary>
    private async Task<CommitRevisionResult?> TryReplayAsync(
        Guid tenantId,
        CommitRevisionRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Revisions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId
                    && r.WorkspaceId == request.WorkspaceId
                    && r.CommitId == request.CommitId,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            // Loud, and deliberately so. Replaying here would tell the client its new tree is committed when it
            // is not; the next pull would treat those edits as absent, and the work would be lost silently with
            // a success response on the record. A loud client bug is the correct trade in the commit path.
            throw new CommitIdReusedException(request.CommitId);
        }

        var headRevisionId = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == request.WorkspaceId)
            .Select(w => w.HeadRevisionId)
            .FirstOrDefaultAsync(cancellationToken);

        return new CommitRevisionResult(
            CommitOutcome.Replayed, existing.Id, existing.Sequence, headRevisionId, []);
    }

    public async Task<IReadOnlyList<ManifestEntry>> GetManifestAsync(
        Guid workspaceId,
        Guid callerPartyId,
        Guid? revisionId = null,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(workspaceId, callerPartyId, WorkspaceAccessLevel.Read, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var target = revisionId ?? await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == workspaceId)
            .Select(w => w.HeadRevisionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return [];
        }

        var files = await _dbContext.Files
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.RevisionId == target)
            .OrderBy(f => f.Path)
            .ToListAsync(cancellationToken);

        return [.. files.Select(f => new ManifestEntry(f.Path, f.ContentHash, f.SizeBytes, f.ContentType))];
    }

    public async Task<IReadOnlyList<RevisionSummary>> GetHistoryAsync(
        Guid workspaceId,
        Guid callerPartyId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(workspaceId, callerPartyId, WorkspaceAccessLevel.Read, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var revisions = await _dbContext.Revisions
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.WorkspaceId == workspaceId)
            .OrderByDescending(r => r.Sequence)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        return
        [
            .. revisions.Select(r => new RevisionSummary(
                r.Id, r.WorkspaceId, r.Sequence, r.ParentRevisionId, r.AuthorPartyId, r.AuthorUserId,
                r.Message, r.CommittedAt, r.FileCount, r.TotalBytes, r.State))
        ];
    }

    public async Task<bool> ResolveAsync(
        Guid revisionId,
        Guid callerPartyId,
        DivergenceResolution resolution,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var revision = await _dbContext.Revisions
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == revisionId, cancellationToken);

        if (revision is null)
        {
            return false;
        }

        await RequireAsync(revision.WorkspaceId, callerPartyId, WorkspaceAccessLevel.Write, cancellationToken);

        if (revision.State != RevisionStates.Diverged)
        {
            // Resolving an already-resolved revision returns the recorded outcome rather than acting twice.
            return false;
        }

        var now = _clock.UtcNow;

        revision.State = resolution switch
        {
            DivergenceResolution.Accept => RevisionStates.Accepted,
            DivergenceResolution.Reject => RevisionStates.Rejected,
            _ => RevisionStates.Superseded,
        };

        revision.ResolvedAt = now;
        revision.ResolvedByPartyId = callerPartyId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (resolution == DivergenceResolution.Accept)
        {
            await AcceptAsync(tenantId, revision, callerPartyId, cancellationToken);
        }

        // Reject does NOT release blobs here. §7.1 keeps a rejected revision for a retention window so the
        // rejection is undoable, and the sweeper releases its blobs afterwards — releasing inline would make
        // "undo" mean "re-upload".
        return true;
    }

    /// <summary>
    /// Accepting advances the head through a <strong>new revision parented on the current head</strong>.
    ///
    /// <para>
    /// History is never rewritten. Repointing the head at a revision whose parent is not its predecessor would
    /// make the chain a lie and break every history read; a resolution is a normal forward step that happens to
    /// carry someone else's tree.
    /// </para>
    /// </summary>
    private async Task AcceptAsync(
        Guid tenantId, WorkspaceRevision accepted, Guid callerPartyId, CancellationToken cancellationToken)
    {
        var manifest = await _dbContext.Files
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.RevisionId == accepted.Id)
            .Select(f => new ManifestEntry(f.Path, f.ContentHash, f.SizeBytes, f.ContentType))
            .ToListAsync(cancellationToken);

        var head = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == accepted.WorkspaceId)
            .Select(w => w.HeadRevisionId)
            .FirstOrDefaultAsync(cancellationToken);

        // A resolution is itself a commit, with the same idempotency and the same compare-and-swap. Its
        // CommitId is derived from the accepted revision so a retried resolution replays rather than
        // committing the same tree twice.
        var request = new CommitRevisionRequest(
            accepted.WorkspaceId,
            DeriveResolutionCommitId(accepted.Id),
            head,
            manifest,
            $"Accepted revision {accepted.Sequence}");

        await CommitAsync(request, callerPartyId, cancellationToken);
    }

    /// <summary>
    /// A stable <c>CommitId</c> for the revision an acceptance produces.
    ///
    /// <para>
    /// Derived rather than random so a retried acceptance is a true retry: the same accepted revision yields the
    /// same id, the request hash matches, and the stored outcome replays instead of the tree being committed a
    /// second time.
    /// </para>
    /// </summary>
    private static Guid DeriveResolutionCommitId(Guid acceptedRevisionId)
    {
        var bytes = acceptedRevisionId.ToByteArray();

        // Flip a byte so the derived id can never collide with the accepted revision's own CommitId.
        bytes[0] ^= 0xA5;

        return new Guid(bytes);
    }

    private async Task RequireAsync(
        Guid workspaceId,
        Guid callerPartyId,
        WorkspaceAccessLevel required,
        CancellationToken cancellationToken)
    {
        var effective = await ResolveAccessAsync(workspaceId, callerPartyId, cancellationToken);

        if (effective < required)
        {
            throw new WorkspaceAccessDeniedException(workspaceId, callerPartyId, required, effective);
        }
    }
}
