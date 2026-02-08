using Aonik.Application.Models.Notifications;

namespace Aonik.Application.Abstractions.Notifications;

public interface INotificationTemplateService
{
    Task<RenderNotificationTemplateResult> RenderAsync(
        RenderNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);
}
