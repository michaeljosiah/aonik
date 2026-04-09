using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Notifications;

internal sealed class UserNotificationWriter : IUserNotificationWriter
{
    private readonly INotificationService _notificationService;

    public UserNotificationWriter(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task WriteForUserAsync(UserNotificationWriteRequest request, CancellationToken cancellationToken = default)
    {
        await _notificationService.CreateForUserAsync(
            new CreateNotificationRequest(
                request.TenantId,
                request.UserId,
                request.Type,
                request.Source,
                request.Title,
                request.Body,
                request.Severity,
                request.ActionUrl,
                request.CorrelationId,
                request.AiRunId,
                request.MetadataJson,
                request.Channel),
            cancellationToken);
    }
}
