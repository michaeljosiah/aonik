using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Compliance.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Compliance;

public class AuditLogWriter : IAuditLogWriter
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IClock _clock;

    public AuditLogWriter(
        IAonikDbContext dbContext,
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
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.TryGetCurrentTenantId(out var tid) ? tid : Guid.Empty;
        var userId = _currentUserProvider.GetCurrentUserId() ?? Guid.Empty;
        var correlationId = _correlationContext.CorrelationId ?? string.Empty;

        var auditLog = new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            TenantId = tenantId,
            Timestamp = _clock.UtcNow,
            ActorType = "User",
            ActorId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            DetailsJson = detailsJson ?? string.Empty,
            CorrelationId = correlationId,
            CreatedAt = _clock.UtcNow,
            CreatedBy = userId
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
