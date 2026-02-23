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

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = resolvedTenantId,
            Timestamp = _clock.UtcNow,
            ActorType = "User",
            ActorId = resolvedActorId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            DetailsJson = detailsJson ?? string.Empty,
            CorrelationId = resolvedCorrelationId,
            CreatedAt = _clock.UtcNow,
            CreatedBy = resolvedActorId
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
