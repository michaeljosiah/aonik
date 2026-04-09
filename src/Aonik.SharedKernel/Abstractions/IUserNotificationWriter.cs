namespace Aonik.SharedKernel.Abstractions;

public record UserNotificationWriteRequest(
    Guid TenantId,
    Guid UserId,
    string Type,
    string Source,
    string Title,
    string Body,
    string Severity,
    string? ActionUrl,
    string? CorrelationId,
    Guid? AiRunId,
    string? MetadataJson,
    string Channel = "InApp");

public interface IUserNotificationWriter
{
    Task WriteForUserAsync(UserNotificationWriteRequest request, CancellationToken cancellationToken = default);
}
