namespace Aonik.Application.Abstractions.Notifications;

public interface INotificationTemplateRenderer
{
    Task<string> RenderAsync(
        string template,
        object? model,
        CancellationToken cancellationToken = default);
}
