using Microsoft.EntityFrameworkCore;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Notifications;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Notifications;
using Aonik.Domain.Notifications.Entities;

namespace Aonik.Application.Services.Notifications;

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly INotificationTemplateRenderer _templateRenderer;

    public NotificationTemplateService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        INotificationTemplateRenderer templateRenderer)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _templateRenderer = templateRenderer;
    }

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
