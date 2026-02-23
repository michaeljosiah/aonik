using Aonik.Platform.Contracts.Models.Notifications;

namespace Aonik.Platform.Contracts.Services.Notifications;

public interface INotificationTemplateService
{
    Task<RenderNotificationTemplateResult> RenderAsync(
        RenderNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);
}
