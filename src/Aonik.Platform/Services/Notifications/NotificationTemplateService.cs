using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Persistence;

namespace Aonik.Platform.Services.Notifications;

internal class NotificationTemplateService : INotificationTemplateService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly INotificationTemplateRenderer _templateRenderer;

    public NotificationTemplateService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        INotificationTemplateRenderer templateRenderer)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _templateRenderer = templateRenderer;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Render (existing)
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<RenderNotificationTemplateResult> RenderAsync(
        RenderNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var binding = await _dbContext.NotificationTemplateBindings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.TemplateName == request.TemplateName
                     && x.Channel == request.Channel
                     && x.IsEnabled,
                cancellationToken);

        var template = await ResolveTemplateAsync(tenantId, request, binding, cancellationToken);

        if (template == null)
            throw new InvalidOperationException(
                $"Notification template '{request.TemplateName}' ({request.Channel}) not found for tenant {tenantId}.");

        var subject = await RenderSubjectAsync(template, request.Model, cancellationToken);
        var body = await _templateRenderer.RenderAsync(template.BodyTemplate, request.Model, cancellationToken);

        NotificationTemplate? baseTemplate = null;
        if (binding?.BaseTemplateId != null)
        {
            baseTemplate = await _dbContext.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == binding.BaseTemplateId, cancellationToken);
        }

        if (baseTemplate != null)
        {
            var baseModel = new Dictionary<string, object?>
            {
                ["content"] = body,
                ["model"] = request.Model
            };

            body = await _templateRenderer.RenderAsync(baseTemplate.BodyTemplate, baseModel, cancellationToken);

            if (string.IsNullOrWhiteSpace(subject) && !string.IsNullOrWhiteSpace(baseTemplate.SubjectTemplate))
            {
                subject = await _templateRenderer.RenderAsync(baseTemplate.SubjectTemplate, baseModel, cancellationToken);
            }
        }

        return new RenderNotificationTemplateResult(
            subject,
            body,
            template.Id,
            baseTemplate?.Id);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Template CRUD
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<List<NotificationTemplateSummary>> ListTemplatesAsync(
        string? channel = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.NotificationTemplates
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId || (x.TenantId == null && x.IsShared));

        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(x => x.Channel == channel);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        return await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Channel)
            .Select(x => new NotificationTemplateSummary(
                x.Id,
                x.Name,
                x.Channel,
                x.Description,
                x.IsShared,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationTemplateResponse?> GetTemplateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var template = await _dbContext.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && (x.TenantId == tenantId || (x.TenantId == null && x.IsShared)),
                cancellationToken);

        return template == null ? null : MapToResponse(template);
    }

    public async Task<NotificationTemplateResponse> CreateTemplateAsync(
        CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var template = new NotificationTemplate
        {
            TenantId = request.IsShared ? null : tenantId,
            Name = request.Name,
            Channel = request.Channel,
            SubjectTemplate = request.SubjectTemplate,
            BodyTemplate = request.BodyTemplate,
            Description = request.Description,
            IsShared = request.IsShared,
            IsActive = request.IsActive
        };

        _dbContext.NotificationTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(template);
    }

    public async Task<NotificationTemplateResponse> UpdateTemplateAsync(
        Guid id,
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var template = await _dbContext.NotificationTemplates
            .FirstOrDefaultAsync(
                x => x.Id == id && (x.TenantId == tenantId || (x.TenantId == null && x.IsShared)),
                cancellationToken)
            ?? throw new InvalidOperationException($"Notification template {id} not found.");

        template.SubjectTemplate = request.SubjectTemplate;
        template.BodyTemplate = request.BodyTemplate;
        template.Description = request.Description;
        template.IsShared = request.IsShared;
        template.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(template);
    }

    public async Task DeleteTemplateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var template = await _dbContext.NotificationTemplates
            .FirstOrDefaultAsync(
                x => x.Id == id && (x.TenantId == tenantId || (x.TenantId == null && x.IsShared)),
                cancellationToken)
            ?? throw new InvalidOperationException($"Notification template {id} not found.");

        _dbContext.NotificationTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PreviewNotificationTemplateResponse> PreviewTemplateAsync(
        PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        object? model = null;
        if (!string.IsNullOrWhiteSpace(request.SampleModelJson))
        {
            model = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                request.SampleModelJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var subject = string.IsNullOrWhiteSpace(request.SubjectTemplate)
            ? string.Empty
            : await _templateRenderer.RenderAsync(request.SubjectTemplate, model, cancellationToken);

        var body = await _templateRenderer.RenderAsync(request.BodyTemplate, model, cancellationToken);

        return new PreviewNotificationTemplateResponse(subject, body);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Binding CRUD
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<List<NotificationTemplateBindingResponse>> ListBindingsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _dbContext.NotificationTemplateBindings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.TemplateName)
            .ThenBy(x => x.Channel)
            .Select(x => new NotificationTemplateBindingResponse(
                x.Id,
                x.TenantId,
                x.TemplateName,
                x.Channel,
                x.BaseTemplateId,
                x.OverrideTemplateId,
                x.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationTemplateBindingResponse> CreateBindingAsync(
        CreateNotificationTemplateBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var binding = new NotificationTemplateBinding
        {
            TenantId = tenantId,
            TemplateName = request.TemplateName,
            Channel = request.Channel,
            BaseTemplateId = request.BaseTemplateId,
            OverrideTemplateId = request.OverrideTemplateId,
            IsEnabled = request.IsEnabled
        };

        _dbContext.NotificationTemplateBindings.Add(binding);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToBindingResponse(binding);
    }

    public async Task<NotificationTemplateBindingResponse> UpdateBindingAsync(
        Guid id,
        UpdateNotificationTemplateBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var binding = await _dbContext.NotificationTemplateBindings
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification template binding {id} not found.");

        binding.BaseTemplateId = request.BaseTemplateId;
        binding.OverrideTemplateId = request.OverrideTemplateId;
        binding.IsEnabled = request.IsEnabled;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToBindingResponse(binding);
    }

    public async Task DeleteBindingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var binding = await _dbContext.NotificationTemplateBindings
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification template binding {id} not found.");

        _dbContext.NotificationTemplateBindings.Remove(binding);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═════════════════════════════════════════════════════════════════════════

    private static NotificationTemplateResponse MapToResponse(NotificationTemplate t) =>
        new(t.Id, t.TenantId, t.Name, t.Channel, t.SubjectTemplate, t.BodyTemplate,
            t.Description, t.IsShared, t.IsActive, t.CreatedAt, t.UpdatedAt);

    private static NotificationTemplateBindingResponse MapToBindingResponse(NotificationTemplateBinding b) =>
        new(b.Id, b.TenantId, b.TemplateName, b.Channel, b.BaseTemplateId, b.OverrideTemplateId, b.IsEnabled);

    private async Task<NotificationTemplate?> ResolveTemplateAsync(
        Guid tenantId,
        RenderNotificationTemplateRequest request,
        NotificationTemplateBinding? binding,
        CancellationToken cancellationToken)
    {
        if (binding?.OverrideTemplateId != null)
        {
            return await _dbContext.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == binding.OverrideTemplateId, cancellationToken);
        }

        var tenantTemplate = await _dbContext.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.Name == request.TemplateName
                     && x.Channel == request.Channel
                     && x.IsActive,
                cancellationToken);

        if (tenantTemplate != null)
        {
            return tenantTemplate;
        }

        return await _dbContext.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == null
                     && x.Name == request.TemplateName
                     && x.Channel == request.Channel
                     && x.IsActive
                     && x.IsShared,
                cancellationToken);
    }

    private async Task<string> RenderSubjectAsync(
        NotificationTemplate template,
        object? model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(template.SubjectTemplate))
        {
            return string.Empty;
        }

        return await _templateRenderer.RenderAsync(template.SubjectTemplate, model, cancellationToken);
    }
}
