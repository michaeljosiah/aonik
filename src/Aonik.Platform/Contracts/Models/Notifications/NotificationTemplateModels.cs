namespace Aonik.Platform.Contracts.Models.Notifications;

public record RenderNotificationTemplateRequest(
    string TemplateName,
    string Channel,
    object? Model);

public record RenderNotificationTemplateResult(
    string Subject,
    string Body,
    Guid TemplateId,
    Guid? BaseTemplateId);
