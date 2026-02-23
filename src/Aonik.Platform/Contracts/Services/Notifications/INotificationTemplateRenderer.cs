namespace Aonik.Platform.Contracts.Services.Notifications;

public interface INotificationTemplateRenderer
{
    Task<string> RenderAsync(
        string template,
        object? model,
        CancellationToken cancellationToken = default);
}
