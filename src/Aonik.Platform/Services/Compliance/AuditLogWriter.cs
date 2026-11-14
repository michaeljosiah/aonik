using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Compliance;

internal class AuditLogWriter : IAuditLogWriter
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IClock _clock;

    public AuditLogWriter(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _clock = clock;
    }

    public async Task LogAsync(
        string action,
        string resourceType,
        Guid resourceId,
        Guid tenantId,
        Guid? actorId,
        string? correlationId,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId == Guid.Empty && _tenantProvider.TryGetCurrentTenantId(out var tid)
            ? tid
            : tenantId;
        var resolvedActorId = actorId ?? _currentUserProvider.GetCurrentUserId() ?? Guid.Empty;
        const int correlationIdMaxLength = 200;
        var resolvedCorrelationId = correlationId ?? _correlationContext.CorrelationId ?? string.Empty;

        if (resolvedCorrelationId.Length > correlationIdMaxLength)
        {
            resolvedCorrelationId = resolvedCorrelationId[..correlationIdMaxLength];
        }

        await WriteAsync(action, resourceType, resourceId, resolvedTenantId, resolvedActorId == Guid.Empty ? "System" : "User",
            resolvedActorId, resolvedCorrelationId, detailsJson, cancellationToken);
    }

    public Task LogActorAsync(string action, string resourceType, Guid resourceId, Guid tenantId, string actorType, Guid actorId,
        string? correlationId, string? detailsJson = null, CancellationToken cancellationToken = default)
    {
        if (actorType is not ("User" or "AutomationIdentity" or "System") || (actorType != "System" && actorId == Guid.Empty))
            throw new ArgumentException("Provide an explicit supported audit actor.");
        string resolvedCorrelationId = correlationId ?? _correlationContext.CorrelationId ?? string.Empty;
        return WriteAsync(action, resourceType, resourceId, tenantId, actorType, actorId,
            resolvedCorrelationId.Length > 200 ? resolvedCorrelationId[..200] : resolvedCorrelationId, detailsJson, cancellationToken);
    }

    private async Task WriteAsync(string action, string resourceType, Guid resourceId, Guid tenantId, string actorType, Guid actorId,
        string correlationId, string? detailsJson, CancellationToken cancellationToken)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Timestamp = _clock.UtcNow,
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            DetailsJson = detailsJson ?? string.Empty,
            CorrelationId = correlationId,
            CreatedAt = _clock.UtcNow,
            CreatedBy = actorId == Guid.Empty ? null : actorId
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
