using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Compliance.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Compliance;

public class AuditLogWriter : IAuditLogWriter
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;

    public AuditLogWriter(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
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
            CreatedAt = _clock.UtcNow,
            CreatedBy = userId
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
