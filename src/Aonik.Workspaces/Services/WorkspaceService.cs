using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Workspace lifecycle, and the quota that moves with it (Spec 089 §9).
/// </summary>
internal sealed class WorkspaceService : IWorkspaceService, IWorkspaceReader
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IBlobPossessionService _possessions;
    private readonly IUsageMeter _meter;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(
        IWorkspaceDataContext dbContext,
        IBlobPossessionService possessions,
        IUsageMeter meter,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<WorkspaceService> logger)
    {
        _dbContext = dbContext;
        _possessions = possessions;
        _meter = meter;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task<WorkspaceSummary> CreateAsync(
        string kind,
        string name,
        Guid ownerPartyId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var workspaceId = Guid.NewGuid();
        var subscriber = new SubscriberRef(SubscriberKinds.Party, ownerPartyId);

        // Claimed BEFORE the row exists. Creating first and claiming after leaves a workspace that
        // nothing is paying for if the claim is refused, and deleting it to compensate is a rollback
        // path that runs exactly when the system is already under pressure.
        await _meter.ClaimSlotAsync(
            subscriber, WorkspaceMeters.Count, workspaceId.ToString("N"), 1, cancellationToken);

        var workspace = new Workspace
        {
            Id = workspaceId,
            TenantId = tenantId,
            Kind = string.IsNullOrWhiteSpace(kind) ? WorkspaceKinds.World : kind,
            Name = name,
            Slug = Slugify(name),
            OwnerPartyId = ownerPartyId,
            BillingSubscriberKind = subscriber.Kind,
            BillingSubscriberId = subscriber.Id,
            Status = WorkspaceStatuses.Active,
            NextSequence = 1,
        };

        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(workspace);
    }

    public async Task<WorkspaceSummary?> GetAsync(
        Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == workspaceId, cancellationToken);

        return workspace is null ? null : ToSummary(workspace);
    }

    public async Task<IReadOnlyList<WorkspaceSummary>> ListForOwnerAsync(
        Guid ownerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var workspaces = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                && w.OwnerPartyId == ownerPartyId
                && w.Status == WorkspaceStatuses.Active)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

        return [.. workspaces.Select(ToSummary)];
    }

    public async Task<bool> RenameAsync(
        Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == workspaceId, cancellationToken);

        if (workspace is null)
        {
            return false;
        }

        workspace.Name = name;
        workspace.Slug = Slugify(name);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Transfer is a claim migration (§9.2): <strong>claim against the recipient first, release the sender
    /// second</strong>.
    ///
    /// <para>
    /// Never the reverse. Releasing first opens a window in which a concurrent claim consumes the freed
    /// allowance, and the transfer then fails with the original owner's claim already gone — the workspace
    /// stranded between owners and nobody holding its bytes.
    /// </para>
    ///
    /// <para>
    /// Insufficient capacity refuses the transfer with the shortfall named, rather than proceeding and
    /// over-admitting. Before this existed, the sender's claims stayed held indefinitely — paying for storage
    /// they no longer had — while the recipient got it for free with no check that they had the capacity or even
    /// a subscription. Both halves wrong in opposite directions, and the symptom a billing complaint neither
    /// party can explain.
    /// </para>
    /// </summary>
    public async Task<bool> TransferOwnershipAsync(
        Guid workspaceId, Guid newOwnerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == workspaceId, cancellationToken);

        if (workspace is null || workspace.Status != WorkspaceStatuses.Active)
        {
            return false;
        }

        var sender = new SubscriberRef(workspace.BillingSubscriberKind, workspace.BillingSubscriberId);
        var recipient = new SubscriberRef(SubscriberKinds.Party, newOwnerPartyId);

        if (sender.Kind == recipient.Kind && sender.Id == recipient.Id)
        {
            workspace.OwnerPartyId = newOwnerPartyId;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var hashSizes = await ReferencedHashSizesAsync(tenantId, workspaceId, cancellationToken);

        // Claim first. An EntitlementExceededException here propagates with the shortfall, and nothing
        // has moved.
        await _meter.ClaimSlotAsync(
            recipient, WorkspaceMeters.Count, workspaceId.ToString("N"), 1, cancellationToken);

        await _possessions.AcquireAsync(recipient, hashSizes, cancellationToken);

        // Only on success does the sender stop paying.
        await _possessions.ReleaseAsync(sender, [.. hashSizes.Keys], cancellationToken);

        await _meter.ReleaseSlotAsync(
            sender, WorkspaceMeters.Count, workspaceId.ToString("N"), cancellationToken);

        workspace.OwnerPartyId = newOwnerPartyId;
        workspace.BillingSubscriberKind = recipient.Kind;
        workspace.BillingSubscriberId = recipient.Id;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Workspace {WorkspaceId} transferred to {NewOwner}; {HashCount} blob claims migrated.",
            workspaceId, newOwnerPartyId, hashSizes.Count);

        return true;
    }

    /// <summary>
    /// Deletion is the degenerate transfer: release both meters, once.
    ///
    /// <para>
    /// Blobs are dereferenced, never deleted inline — another workspace in the same tenant may have deduped
    /// against them, and the sweeper is what turns a zero count into reclaimed storage.
    /// </para>
    /// </summary>
    public async Task<bool> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == workspaceId, cancellationToken);

        if (workspace is null || workspace.Status != WorkspaceStatuses.Active)
        {
            return false;
        }

        var subscriber = new SubscriberRef(workspace.BillingSubscriberKind, workspace.BillingSubscriberId);
        var hashSizes = await ReferencedHashSizesAsync(tenantId, workspaceId, cancellationToken);

        await _possessions.ReleaseAsync(subscriber, [.. hashSizes.Keys], cancellationToken);

        await _meter.ReleaseSlotAsync(
            subscriber, WorkspaceMeters.Count, workspaceId.ToString("N"), cancellationToken);

        // Physical references drop too, which is what makes the blobs sweepable — but only for hashes
        // no other revision of any workspace still names, which RefCount already tracks.
        var hashes = await ReferencedHashesAsync(tenantId, workspaceId, cancellationToken);
        workspace.Status = WorkspaceStatuses.Deleted;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── IWorkspaceReader ─────────────────────────────────────────────────

    public async Task<bool> IsOwnedByAsync(
        Guid workspaceId, Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _dbContext.Workspaces
            .AsNoTracking()
            .AnyAsync(w => w.TenantId == tenantId
                && w.Id == workspaceId
                && w.OwnerPartyId == partyId
                && w.Status == WorkspaceStatuses.Active, cancellationToken);
    }

    public async Task<long> GetTotalBytesAsync(
        Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == workspaceId)
            .Select(w => w.TotalBytes)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountForOwnerAsync(
        Guid ownerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _dbContext.Workspaces
            .AsNoTracking()
            .CountAsync(w => w.TenantId == tenantId
                && w.OwnerPartyId == ownerPartyId
                && w.Status == WorkspaceStatuses.Active, cancellationToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Every hash any revision of this workspace names, with its size.
    ///
    /// <para>
    /// All revisions, not just the head. A workspace's cost is its whole history — that is what all-history
    /// retention means — and charging only for the head would let a large tree be hidden one commit back.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, long>> ReferencedHashSizesAsync(
        Guid tenantId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var revisionIds = await _dbContext.Revisions
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.WorkspaceId == workspaceId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var files = await _dbContext.Files
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && revisionIds.Contains(f.RevisionId))
            .Select(f => new { f.ContentHash, f.SizeBytes })
            .ToListAsync(cancellationToken);

        return files
            .GroupBy(f => f.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SizeBytes, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<string>> ReferencedHashesAsync(
        Guid tenantId, Guid workspaceId, CancellationToken cancellationToken)
        => [.. (await ReferencedHashSizesAsync(tenantId, workspaceId, cancellationToken)).Keys];

    private static WorkspaceSummary ToSummary(Workspace w)
        => new(w.Id, w.Kind, w.Name, w.Slug, w.OwnerPartyId, w.HeadRevisionId,
            w.FileCount, w.TotalBytes, w.Status);

    private static string Slugify(string name)
    {
        var slug = new string([.. name.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')]);

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-') is { Length: > 0 } trimmed
            ? trimmed[..Math.Min(trimmed.Length, 80)]
            : Guid.NewGuid().ToString("N")[..12];
    }
}
