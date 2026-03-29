namespace Aonik.Platform.Contracts.Models.Notifications;

// ── Render (existing) ────────────────────────────────────────────────────────
public record RenderNotificationTemplateRequest(
    string TemplateName,
    string Channel,
    object? Model);

public record RenderNotificationTemplateResult(
    string Subject,
    string Body,
    Guid TemplateId,
    Guid? BaseTemplateId);

// ── Template CRUD ────────────────────────────────────────────────────────────
public record NotificationTemplateResponse(
    Guid Id,
    Guid? TenantId,
    string Name,
    string Channel,
    string SubjectTemplate,
    string BodyTemplate,
    string Description,
    bool IsShared,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record NotificationTemplateSummary(
    Guid Id,
    string Name,
    string Channel,
    string Description,
    bool IsShared,
    bool IsActive);

public record CreateNotificationTemplateRequest(
    string Name,
    string Channel,
    string SubjectTemplate,
    string BodyTemplate,
    string Description,
    bool IsShared,
    bool IsActive);

public record UpdateNotificationTemplateRequest(
    string SubjectTemplate,
    string BodyTemplate,
    string Description,
    bool IsShared,
    bool IsActive);

public record PreviewNotificationTemplateRequest(
    string SubjectTemplate,
    string BodyTemplate,
    string SampleModelJson);

public record PreviewNotificationTemplateResponse(
    string Subject,
    string Body);

// ── Binding CRUD ─────────────────────────────────────────────────────────────
public record NotificationTemplateBindingResponse(
    Guid Id,
    Guid TenantId,
    string TemplateName,
    string Channel,
    Guid? BaseTemplateId,
    Guid? OverrideTemplateId,
    bool IsEnabled);

public record CreateNotificationTemplateBindingRequest(
    string TemplateName,
    string Channel,
    Guid? BaseTemplateId,
    Guid? OverrideTemplateId,
    bool IsEnabled);

public record UpdateNotificationTemplateBindingRequest(
    Guid? BaseTemplateId,
    Guid? OverrideTemplateId,
    bool IsEnabled);
