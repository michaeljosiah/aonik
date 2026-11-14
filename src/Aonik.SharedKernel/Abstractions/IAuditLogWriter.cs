namespace Aonik.SharedKernel.Abstractions;

public interface IAuditLogWriter
{
    Task LogActorAsync(string action, string resourceType, Guid resourceId, Guid tenantId, string actorType, Guid actorId,
        string? correlationId, string? detailsJson = null, CancellationToken cancellationToken = default)
        => actorType == "User" ? LogAsync(action, resourceType, resourceId, tenantId, actorId, correlationId, detailsJson, cancellationToken)
            : throw new InvalidOperationException("This audit adapter cannot record non-human actors.");

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
