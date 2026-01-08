namespace Aonik.Application.Services.Compliance;

public interface IAuditLogWriter
{
    Task LogAsync(
        string action,
        string resourceType,
        Guid resourceId,
        string? detailsJson = null,
        CancellationToken cancellationToken = default);
}
