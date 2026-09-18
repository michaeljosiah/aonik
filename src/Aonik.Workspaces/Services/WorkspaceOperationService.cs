using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Workspaces.Services;

/// <summary>What a product asks to begin: everything the engine's record carries, verbatim.</summary>
public sealed record BeginOperationRequest(
    Guid WorkspaceId,
    string Key,
    string Fingerprint,
    string Action,
    string ContextJson,
    string ResourceJson,
    string? ResultJson);

public sealed record OperationRecord(
    Guid WorkspaceId,
    string Key,
    string Fingerprint,
    string Action,
    string Status,
    string ContextJson,
    string ResourceJson,
    string? ResultJson,
    DateTime StartedAt,
    DateTime? CompletedAt)
{
    internal static OperationRecord From(WorkspaceOperation row) => new(
        row.WorkspaceId, row.Key, row.Fingerprint, row.Action, row.Status,
        row.ContextJson, row.ResourceJson, row.ResultJson, row.StartedAt, row.CompletedAt);
}

/// <summary>A key already begun with a different fingerprint, or completed with a different result.</summary>
public sealed class OperationIdentityConflictException : Exception
{
    public OperationIdentityConflictException(string key, string message) : base(message) => Key = key;

    public string Key { get; }
}

/// <summary>
/// The engine's operation store, held here (aonik#327): insert-if-absent by key, complete once,
/// read back. Durable and atomic across hosts because the unique index on the key is, not because
/// any host remembers anything.
///
/// <para>
/// Access is the workspace's: beginning or completing an operation needs <c>Write</c> access to
/// the workspace it names, reading one needs <c>Read</c>. A key nobody may read looks like no key
/// at all. Rows are never deleted — a started row that was never completed is the durable
/// "uncertain" the engine expects, and the product reconciles it against the record, the jobs and
/// the reservations rather than retrying blind.
/// </para>
/// </summary>
public interface IWorkspaceOperationService
{
    Task<(bool Inserted, OperationRecord Operation)> BeginAsync(BeginOperationRequest request, Guid callerPartyId, CancellationToken cancellationToken = default);

    Task<OperationRecord> CompleteAsync(string key, string fingerprint, string resultJson, Guid callerPartyId, CancellationToken cancellationToken = default);

    Task<OperationRecord?> ReadAsync(string key, Guid callerPartyId, CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceOperationService : IWorkspaceOperationService
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly IWorkspaceSyncService _access;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<WorkspaceOperationService> _logger;

    public WorkspaceOperationService(
        IWorkspaceDataContext dbContext,
        IWorkspaceSyncService access,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<WorkspaceOperationService> logger)
    {
        _dbContext = dbContext;
        _access = access;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task<(bool Inserted, OperationRecord Operation)> BeginAsync(BeginOperationRequest request, Guid callerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await RequireAsync(request.WorkspaceId, callerPartyId, WorkspaceAccessLevel.Write, cancellationToken);

        var existing = await FindAsync(tenantId, request.Key, cancellationToken);

        if (existing is not null)
        {
            return (false, Same(existing, request));
        }

        var row = new WorkspaceOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkspaceId = request.WorkspaceId,
            Key = request.Key,
            Fingerprint = request.Fingerprint,
            Action = request.Action,
            CallerPartyId = callerPartyId,
            Status = WorkspaceOperationStatuses.Started,
            ContextJson = request.ContextJson,
            ResourceJson = request.ResourceJson,
            ResultJson = request.ResultJson,
            StartedAt = _clock.UtcNow,
        };

        _dbContext.Operations.Add(row);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two hosts began the same operation at once and the index let one in. This host lost:
            // what it finds is the row the other wrote, which is the answer the engine wants.
            _dbContext.Operations.Entry(row).State = EntityState.Detached;
            var winner = await FindAsync(tenantId, request.Key, cancellationToken)
                ?? throw new InvalidOperationException("The operation could not be inserted and cannot be read.");

            _logger.LogDebug("Operation {Key} was begun concurrently; returning the row that won.", request.Key);

            return (false, Same(winner, request));
        }

        return (true, OperationRecord.From(row));
    }

    public async Task<OperationRecord> CompleteAsync(string key, string fingerprint, string resultJson, Guid callerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var row = await _dbContext.Operations
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Key == key, cancellationToken)
            ?? throw new NotFoundException($"No such operation: {key}.");

        await RequireAsync(row.WorkspaceId, callerPartyId, WorkspaceAccessLevel.Write, cancellationToken);

        if (row.Fingerprint != fingerprint)
        {
            throw new OperationIdentityConflictException(key, "The completion names a different request than the one begun.");
        }

        if (row.Status == WorkspaceOperationStatuses.Completed)
        {
            // Completed once. The same result again is a lost response replayed; a different one is a bug
            // somewhere, and the record does not move for it.
            if (!string.Equals(row.ResultJson, resultJson, StringComparison.Ordinal))
            {
                throw new OperationIdentityConflictException(key, "The operation was completed earlier with a different result.");
            }

            return OperationRecord.From(row);
        }

        row.Status = WorkspaceOperationStatuses.Completed;
        row.ResultJson = resultJson;
        row.CompletedAt = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationRecord.From(row);
    }

    public async Task<OperationRecord?> ReadAsync(string key, Guid callerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var row = await FindAsync(tenantId, key, cancellationToken);

        if (row is null)
        {
            return null;
        }

        var access = await _access.ResolveAccessAsync(row.WorkspaceId, callerPartyId, cancellationToken);

        // A key the caller may not read is no key: nothing about another family's world leaks by key.
        return access.Allows(WorkspaceAccessLevel.Read) ? OperationRecord.From(row) : null;
    }

    private async Task RequireAsync(Guid workspaceId, Guid callerPartyId, WorkspaceAccessLevel required, CancellationToken cancellationToken)
    {
        var access = await _access.ResolveAccessAsync(workspaceId, callerPartyId, cancellationToken);

        if (!access.Allows(required))
        {
            throw new WorkspaceAccessDeniedException(workspaceId, callerPartyId, required, access);
        }
    }

    private Task<WorkspaceOperation?> FindAsync(Guid tenantId, string key, CancellationToken cancellationToken)
        => _dbContext.Operations.AsNoTracking().FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Key == key, cancellationToken);

    /// <summary>An existing row is returned only to the request that matches it; a key reused for different content is refused.</summary>
    private static OperationRecord Same(WorkspaceOperation existing, BeginOperationRequest request)
    {
        if (existing.Fingerprint != request.Fingerprint || existing.WorkspaceId != request.WorkspaceId)
        {
            throw new OperationIdentityConflictException(request.Key, "The operation key was begun earlier for a different request.");
        }

        return OperationRecord.From(existing);
    }
}
