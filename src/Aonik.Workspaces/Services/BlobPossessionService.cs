using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Answers <em>may this subscriber stop paying for these bytes?</em> (Spec 089 §9.2).
///
/// <para>
/// A different question from <c>WorkspaceBlob.RefCount</c>, which answers <em>may these bytes be deleted?</em>
/// The two look alike and diverge exactly where it matters: a blob shared by two workspaces belonging to one
/// subscriber has physical refcount 2 and possession count 2, and transferring one workspace away must drop the
/// possession to 1 while the physical count stays 2.
/// </para>
/// </summary>
public interface IBlobPossessionService
{
    /// <summary>
    /// Record that a subscriber's workspace references these hashes, claiming ceiling weight for any they did
    /// not already hold.
    /// </summary>
    Task AcquireAsync(
        SubscriberRef subscriber,
        IReadOnlyDictionary<string, long> hashSizes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drop one workspace's possession, releasing the ceiling claim only for hashes that reach zero.
    /// </summary>
    Task ReleaseAsync(
        SubscriberRef subscriber,
        IReadOnlyCollection<string> contentHashes,
        CancellationToken cancellationToken = default);

    /// <summary>Bytes this subscriber would newly owe for these hashes. Nothing is claimed.</summary>
    Task<long> ProjectedWeightAsync(
        SubscriberRef subscriber,
        IReadOnlyDictionary<string, long> hashSizes,
        CancellationToken cancellationToken = default);
}

internal sealed class BlobPossessionService : IBlobPossessionService
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IUsageMeter _meter;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<BlobPossessionService> _logger;

    public BlobPossessionService(
        IWorkspaceDataContext dbContext,
        IUsageMeter meter,
        ITenantProvider tenantProvider,
        ILogger<BlobPossessionService> logger)
    {
        _dbContext = dbContext;
        _meter = meter;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task AcquireAsync(
        SubscriberRef subscriber,
        IReadOnlyDictionary<string, long> hashSizes,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        foreach (var (hash, size) in hashSizes)
        {
            var possession = await _dbContext.Possessions
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId
                        && p.SubscriberKind == subscriber.Kind
                        && p.SubscriberId == subscriber.Id
                        && p.ContentHash == hash,
                    cancellationToken);

            if (possession is null)
            {
                // First workspace of theirs to hold these bytes, so this is where they start paying.
                // ClaimSlotAsync is idempotent per holder ref, which is the content hash — so ten
                // revisions naming one blob are charged once, for free, by machinery Spec 087 already has.
                await _meter.ClaimSlotAsync(
                    subscriber, WorkspaceMeters.Bytes, hash, size, cancellationToken);

                _dbContext.Possessions.Add(new BlobPossession
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SubscriberKind = subscriber.Kind,
                    SubscriberId = subscriber.Id,
                    ContentHash = hash,
                    SizeBytes = size,
                    WorkspaceCount = 1,
                });

                continue;
            }

            possession.WorkspaceCount += 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(
        SubscriberRef subscriber,
        IReadOnlyCollection<string> contentHashes,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        foreach (var hash in contentHashes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var possession = await _dbContext.Possessions
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId
                        && p.SubscriberKind == subscriber.Kind
                        && p.SubscriberId == subscriber.Id
                        && p.ContentHash == hash,
                    cancellationToken);

            if (possession is null)
            {
                continue;
            }

            possession.WorkspaceCount -= 1;

            if (possession.WorkspaceCount > 0)
            {
                // Still held by another of their workspaces. Releasing the ceiling claim here is the
                // §9.2 hole: the retained workspace's bytes would be completely uncharged while the
                // physical blob cannot be swept and we are still paying for it.
                continue;
            }

            _dbContext.Possessions.Remove(possession);

            await _meter.ReleaseSlotAsync(
                subscriber, WorkspaceMeters.Bytes, hash, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> ProjectedWeightAsync(
        SubscriberRef subscriber,
        IReadOnlyDictionary<string, long> hashSizes,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var hashes = hashSizes.Keys.ToList();

        var alreadyHeld = await _dbContext.Possessions
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && p.SubscriberKind == subscriber.Kind
                && p.SubscriberId == subscriber.Id
                && hashes.Contains(p.ContentHash))
            .Select(p => p.ContentHash)
            .ToListAsync(cancellationToken);

        var heldSet = alreadyHeld.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Only what they do not already hold. A recipient who happens to share content with the sender
        // owes nothing extra for it, which is the same dedupe property the physical store gives.
        return hashSizes
            .Where(pair => !heldSet.Contains(pair.Key))
            .Sum(pair => pair.Value);
    }
}

/// <summary>The Spec 087 meters a workspace holds against (Spec 089 §9).</summary>
public static class WorkspaceMeters
{
    /// <summary>Ceiling — one held slot per workspace, released on delete.</summary>
    public const string Count = "workspaces";

    /// <summary>
    /// <strong>Weighted ceiling</strong>, emphatically not a counter.
    ///
    /// <para>
    /// An earlier draft made this a counter "drawn down as blobs are stored, returned on sweep", and the second
    /// half is not implementable: <c>CommitAsync</c> moves the reservation into <c>Consumed</c>, and
    /// <strong>Consumed never decreases</strong> — correctly, because a counter measures a flow that has happened
    /// and cannot un-happen. A customer uploading 40GB and deleting it would have permanently burned 40GB of a
    /// 50GB allowance while using none of it, and the support ticket would be indistinguishable from a billing
    /// fault. Storage is a <em>level</em>, like a seat count, and a level is what a ceiling models.
    /// </para>
    /// </summary>
    public const string Bytes = "workspace-bytes";
}
