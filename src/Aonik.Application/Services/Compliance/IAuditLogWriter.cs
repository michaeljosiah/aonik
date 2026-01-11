namespace Aonik.Application.Services.Compliance;

public interface IAuditLogWriter
{
    Task LogAsync(
        string action,
        string resourceType,
        Guid resourceId,
        Guid tenantId,
        Guid? actorId,
        string? correlationId,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);
}
