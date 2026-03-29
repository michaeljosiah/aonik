using Aonik.Platform.Contracts.Models.Notifications;

namespace Aonik.Platform.Contracts.Services.Notifications;

public interface INotificationTemplateService
{
    // ── Render ───────────────────────────────────────────────────────────────
    Task<RenderNotificationTemplateResult> RenderAsync(
        RenderNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    // ── Template CRUD ────────────────────────────────────────────────────────
    Task<List<NotificationTemplateSummary>> ListTemplatesAsync(
        string? channel = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateResponse?> GetTemplateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateResponse> CreateTemplateAsync(
        CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateResponse> UpdateTemplateAsync(
        Guid id,
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PreviewNotificationTemplateResponse> PreviewTemplateAsync(
        PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    // ── Binding CRUD ─────────────────────────────────────────────────────────
    Task<List<NotificationTemplateBindingResponse>> ListBindingsAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateBindingResponse> CreateBindingAsync(
        CreateNotificationTemplateBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateBindingResponse> UpdateBindingAsync(
        Guid id,
        UpdateNotificationTemplateBindingRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteBindingAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
