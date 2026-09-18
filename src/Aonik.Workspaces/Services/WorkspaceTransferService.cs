using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Moving bytes in and out of a workspace over the platform's own endpoints (Spec 091 §5–§7).
///
/// <para>
/// Both directions resolve the caller's effective access first, the same way the sync service does for commits,
/// because these are the endpoints a modified client would call directly. Upload needs <c>Write</c> so a reader
/// consumes no quota (Spec 089 §8.1); download needs <c>Read</c> <em>and</em> a revision of this workspace that
/// names the hash, so a content hash never acts as a bearer capability across workspaces (§12).
/// </para>
/// </summary>
internal interface IWorkspaceTransferService
{
    /// <summary>
    /// Store a blob the caller declares by hash and length. The declaration is verified before promotion; a
    /// mismatch discards the staged bytes. Possession is recorded for the workspace's billing subscriber.
    /// </summary>
    Task<BlobStoreResult> UploadAsync(
        Guid workspaceId,
        Guid callerPartyId,
        BlobDeclaration declared,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Open a blob named by a revision of this workspace, or <c>null</c> when no revision names it — the same
    /// answer as for a hash that does not exist, so nothing here confirms what other workspaces hold.
    /// </summary>
    Task<WorkspaceBlobContent?> OpenAsync(
        Guid workspaceId,
        Guid callerPartyId,
        string contentHash,
        CancellationToken cancellationToken = default);
}

internal sealed record WorkspaceBlobContent(Stream Content, long SizeBytes, string? ContentType);

/// <summary>A single-shot upload larger than the multipart threshold; Spec 091 §7's resumable path is for those.</summary>
public sealed class UploadTooLargeForSingleShotException : Exception
{
    public UploadTooLargeForSingleShotException(long declared, long limit)
        : base($"A single-shot upload may declare at most {limit} bytes; {declared} were declared. Use the resumable upload for larger blobs.")
    {
        Declared = declared;
        Limit = limit;
    }

    public long Declared { get; }
    public long Limit { get; }
}

internal sealed class WorkspaceTransferService : IWorkspaceTransferService
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IWorkspaceSyncService _sync;
    private readonly IWorkspaceBlobService _blobs;
    private readonly IFileStore _fileStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly WorkspaceOptions _options;

    public WorkspaceTransferService(
        IWorkspaceDataContext dbContext,
        IWorkspaceSyncService sync,
        IWorkspaceBlobService blobs,
        IFileStore fileStore,
        ITenantProvider tenantProvider,
        IOptions<WorkspaceOptions> options)
    {
        _dbContext = dbContext;
        _sync = sync;
        _blobs = blobs;
        _fileStore = fileStore;
        _tenantProvider = tenantProvider;
        _options = options.Value;
    }

    public async Task<BlobStoreResult> UploadAsync(
        Guid workspaceId,
        Guid callerPartyId,
        BlobDeclaration declared,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(workspaceId, callerPartyId, WorkspaceAccessLevel.Write, cancellationToken);

        if (declared.SizeBytes > _options.MultipartThresholdBytes)
        {
            throw new UploadTooLargeForSingleShotException(declared.SizeBytes, _options.MultipartThresholdBytes);
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var subscriber = await WorkspaceBilling.SubscriberForAsync(_dbContext, tenantId, workspaceId, cancellationToken);

        return await _blobs.StoreAsync(subscriber, content, declared, cancellationToken);
    }

    public async Task<WorkspaceBlobContent?> OpenAsync(
        Guid workspaceId,
        Guid callerPartyId,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        await RequireAsync(workspaceId, callerPartyId, WorkspaceAccessLevel.Read, cancellationToken);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalised = contentHash.ToLowerInvariant();

        // Reachable through THIS workspace: a file in any of its revisions names the hash. A divergent or
        // rejected revision counts — its bytes are still this workspace's history — but another workspace's
        // do not, however identical.
        var named = await _dbContext.Files
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.ContentHash == normalised)
            .Join(
                _dbContext.Revisions.AsNoTracking().Where(r => r.TenantId == tenantId && r.WorkspaceId == workspaceId),
                file => file.RevisionId,
                revision => revision.Id,
                (file, _) => new { file.ContentType })
            .FirstOrDefaultAsync(cancellationToken);

        if (named is null)
        {
            return null;
        }

        var blob = await _dbContext.Blobs
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.ContentHash == normalised)
            .Select(b => new { b.StorageKey, b.SizeBytes })
            .FirstOrDefaultAsync(cancellationToken);

        if (blob is null)
        {
            return null;
        }

        var stream = await _fileStore.OpenReadAsync(blob.StorageKey, cancellationToken);

        return stream is null ? null : new WorkspaceBlobContent(stream, blob.SizeBytes, named.ContentType);
    }

    private async Task RequireAsync(
        Guid workspaceId,
        Guid callerPartyId,
        WorkspaceAccessLevel required,
        CancellationToken cancellationToken)
    {
        var effective = await _sync.ResolveAccessAsync(workspaceId, callerPartyId, cancellationToken);

        if (effective < required)
        {
            throw new WorkspaceAccessDeniedException(workspaceId, callerPartyId, required, effective);
        }
    }
}
