using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Resumable multipart upload for large media (Spec 091 §7).
///
/// <para>
/// Small blobs do not come through here — below the threshold the part bookkeeping costs more than the retry it
/// saves. Above it, a failed transfer is painful on a domestic uplink, and starting a 4GB take again from zero is
/// the single most likely reason a customer concludes sync does not work.
/// </para>
/// </summary>
public interface IWorkspaceUploadService
{
    /// <summary>
    /// Open a session, or resume one already open for the same declared hash.
    ///
    /// <para>
    /// Resuming is keyed on the declared hash rather than a session id the client has to keep, because a client
    /// that crashed may not have kept it — and the hash is a thing it can always recompute from the file.
    /// </para>
    /// </summary>
    Task<UploadSession> BeginAsync(
        SubscriberRef subscriber,
        string declaredHash,
        long declaredLength,
        CancellationToken cancellationToken = default);

    /// <summary>Which parts the server does not hold — the negotiation question, one level down.</summary>
    Task<IReadOnlyList<int>> GetMissingPartsAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept one part. Re-sending a part the server already holds is a no-op, so a client that lost its
    /// acknowledgement does not corrupt the assembly by sending twice.
    /// </summary>
    Task UploadPartAsync(
        Guid sessionId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assemble, verify against the declared hash, and promote.
    ///
    /// <para>
    /// A mismatch discards the staged object and aborts the session — nothing partial is ever promoted.
    /// </para>
    /// </summary>
    Task<BlobStoreResult> CompleteAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    Task<bool> AbortAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <param name="MissingParts">Empty on a fresh session means the blob is one part.</param>
public sealed record UploadSession(
    Guid SessionId,
    int PartSizeBytes,
    int TotalParts,
    IReadOnlyList<int> MissingParts);

internal sealed class WorkspaceUploadService : IWorkspaceUploadService
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IWorkspaceBlobService _blobs;
    private readonly IFileStore _fileStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<WorkspaceUploadService> _logger;

    public WorkspaceUploadService(
        IWorkspaceDataContext dbContext,
        IWorkspaceBlobService blobs,
        IFileStore fileStore,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<WorkspaceOptions> options,
        ILogger<WorkspaceUploadService> logger)
    {
        _dbContext = dbContext;
        _blobs = blobs;
        _fileStore = fileStore;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UploadSession> BeginAsync(
        SubscriberRef subscriber,
        string declaredHash,
        long declaredLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredHash);

        if (declaredLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(declaredLength), "An upload must declare a positive length to be bounded by.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;
        var hash = declaredHash.ToLowerInvariant();

        var existing = await _dbContext.UploadSessions
            .Include(s => s.Parts)
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId
                    && s.SubscriberKind == subscriber.Kind
                    && s.SubscriberId == subscriber.Id
                    && s.DeclaredHash == hash
                    && s.Status == UploadSessionStatuses.Open
                    && s.ExpiresAt > now,
                cancellationToken);

        if (existing is not null)
        {
            // The resume path, and the whole point of the phase: what comes back is the set of parts
            // still needed, not an instruction to start again.
            return Describe(existing);
        }

        var session = new BlobUploadSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = subscriber.Kind,
            SubscriberId = subscriber.Id,
            DeclaredHash = hash,
            DeclaredLength = declaredLength,
            PartSizeBytes = _options.PartSizeBytes,
            ReceivedBytes = 0,
            Status = UploadSessionStatuses.Open,
            ExpiresAt = now.AddHours(_options.UploadSessionHours),
        };

        _dbContext.UploadSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Describe(session);
    }

    public async Task<IReadOnlyList<int>> GetMissingPartsAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
        => Describe(await RequireOpenAsync(sessionId, cancellationToken)).MissingParts;

    public async Task UploadPartAsync(
        Guid sessionId,
        int partNumber,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var session = await RequireOpenAsync(sessionId, cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (partNumber < 1 || partNumber > TotalParts(session))
        {
            throw new ArgumentOutOfRangeException(
                nameof(partNumber),
                $"Part {partNumber} is outside the {TotalParts(session)} parts this upload declared.");
        }

        if (session.Parts.Any(p => p.PartNumber == partNumber))
        {
            // Idempotent. A client that lost its acknowledgement re-sends, and re-sending must not
            // append a second copy into the assembly.
            _logger.LogDebug(
                "Part {PartNumber} of session {SessionId} is already held; ignoring the re-send.",
                partNumber, sessionId);

            return;
        }

        var key = PartKey(tenantId, session.Id, partNumber);

        // Bounded by what remains of the declaration, so a caller cannot outrun the quota check by
        // streaming more than it said. The stage call throws mid-write and nothing is assembled.
        var remaining = session.DeclaredLength - session.ReceivedBytes;
        await using var bounded = new BoundedReadStream(content, remaining);

        StagedBlob staged;

        try
        {
            staged = await _fileStore.StageAsync(tenantId, bounded, cancellationToken);
        }
        catch (DeclaredLengthExceededException)
        {
            await AbortAsync(sessionId, cancellationToken);
            throw;
        }

        // StageAsync chose its own temp key; move the part to a deterministic one so assembly can find
        // it after a crash without keeping the staging key in memory.
        var moved = await _fileStore.PromoteAsync(staged, key, cancellationToken);

        _dbContext.UploadParts.Add(new BlobUploadPart
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            PartNumber = partNumber,
            SizeBytes = moved.SizeBytes,
            StorageKey = key,
        });

        session.ReceivedBytes += moved.SizeBytes;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BlobStoreResult> CompleteAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await RequireOpenAsync(sessionId, cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var missing = Describe(session).MissingParts;

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Upload {sessionId} is missing {missing.Count} parts and cannot be assembled.");
        }

        var ordered = session.Parts.OrderBy(p => p.PartNumber).ToList();

        // Streamed, never buffered. Concatenating by reading into memory would hold a whole take at the
        // one moment the file is largest.
        await using var assembled = new ConcatenatingStream(
            [.. ordered.Select<BlobUploadPart, Func<CancellationToken, Task<Stream?>>>(
                part => ct => _fileStore.OpenReadAsync(part.StorageKey, ct))]);

        var staged = await _fileStore.StageAsync(tenantId, assembled, cancellationToken);

        if (!string.Equals(staged.ContentHash, session.DeclaredHash, StringComparison.OrdinalIgnoreCase))
        {
            // Verification on promote, not on trust. Without this a client could upload its own bytes
            // under someone else's hash and then read that hash back as though it were theirs.
            await _fileStore.DeleteAsync(staged.TempKey, cancellationToken);
            await AbortAsync(sessionId, cancellationToken);

            throw new UploadHashMismatchException(session.DeclaredHash, staged.ContentHash);
        }

        var contentKey = WorkspaceBlobService.ContentKeyFor(tenantId, staged.ContentHash);
        await _fileStore.PromoteAsync(staged, contentKey, cancellationToken);

        // Registered through the ordinary path, so possession, dedupe and the deletion-claim handling
        // are the same as a single-shot upload — a second implementation of them would be a second
        // place for the §6 leak to reappear.
        await using var promoted = await _fileStore.OpenReadAsync(contentKey, cancellationToken)
            ?? throw new InvalidOperationException($"Promoted object {contentKey} is not readable.");

        var result = await _blobs.StoreAsync(
            new SubscriberRef(session.SubscriberKind, session.SubscriberId), promoted, cancellationToken);

        session.Status = UploadSessionStatuses.Completed;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await DeletePartsAsync(session, cancellationToken);

        return result;
    }

    public async Task<bool> AbortAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var session = await _dbContext.UploadSessions
            .Include(s => s.Parts)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sessionId, cancellationToken);

        if (session is null || session.Status != UploadSessionStatuses.Open)
        {
            return false;
        }

        session.Status = UploadSessionStatuses.Aborted;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await DeletePartsAsync(session, cancellationToken);
        return true;
    }

    private async Task DeletePartsAsync(BlobUploadSession session, CancellationToken cancellationToken)
    {
        foreach (var part in session.Parts)
        {
            try
            {
                await _fileStore.DeleteAsync(part.StorageKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The row survives, so the sweeper finds it again. Losing the row here would strand the
                // object exactly as the pre-091 staging path did.
                _logger.LogWarning(ex,
                    "Could not delete upload part {StorageKey}; leaving it for the sweeper.",
                    part.StorageKey);

                return;
            }
        }

        _dbContext.UploadParts.RemoveRange(session.Parts);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<BlobUploadSession> RequireOpenAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var session = await _dbContext.UploadSessions
            .Include(s => s.Parts)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Upload session {sessionId} does not exist.");

        if (session.Status != UploadSessionStatuses.Open)
        {
            throw new InvalidOperationException(
                $"Upload session {sessionId} is {session.Status} and cannot accept parts.");
        }

        return session;
    }

    private static int TotalParts(BlobUploadSession session)
        => (int)Math.Max(1, (session.DeclaredLength + session.PartSizeBytes - 1) / session.PartSizeBytes);

    private static UploadSession Describe(BlobUploadSession session)
    {
        var total = TotalParts(session);
        var held = session.Parts.Select(p => p.PartNumber).ToHashSet();

        return new UploadSession(
            session.Id,
            session.PartSizeBytes,
            total,
            [.. Enumerable.Range(1, total).Where(n => !held.Contains(n))]);
    }

    private static string PartKey(Guid tenantId, Guid sessionId, int partNumber)
        => $"workspaces/{tenantId:N}/uploads/{sessionId:N}/{partNumber:D6}";
}
