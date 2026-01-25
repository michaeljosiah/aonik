using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Features;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Features.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Features;

public class TenantFeatureService : ITenantFeatureService
{
    private readonly IAonikDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IPermissionService _permissionService;
    private readonly ITenantContext _tenantContext;

    public TenantFeatureService(
        IAonikDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IAuditLogWriter auditLogWriter,
        IPermissionService permissionService,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _auditLogWriter = auditLogWriter;
        _permissionService = permissionService;
        _tenantContext = tenantContext;
    }

    public async Task<TenantFeatureList> GetTenantFeaturesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Read", cancellationToken);
        await EnsureTenantExistsAsync(tenantId, cancellationToken);

        var features = await _dbContext.TenantFeatures
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.FeatureName)
            .ToListAsync(cancellationToken);

        var states = features
            .Select(feature => new TenantFeatureState(
                feature.FeatureName,
                feature.IsEnabled,
                feature.UpdatedAt))
            .ToList();

        return new TenantFeatureList(tenantId, states);
    }

    public async Task<TenantFeatureList> UpsertTenantFeaturesAsync(
        Guid tenantId,
        IReadOnlyList<TenantFeatureToggle> toggles,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        await EnsureTenantExistsAsync(tenantId, cancellationToken);

        if (toggles.Count == 0)
        {
            return new TenantFeatureList(tenantId, Array.Empty<TenantFeatureState>());
        }

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var featureNames = toggles
            .Select(toggle => toggle.FeatureName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existing = await _dbContext.TenantFeatures
            .Where(x => x.TenantId == tenantId && featureNames.Contains(x.FeatureName))
            .ToListAsync(cancellationToken);

        var existingMap = existing.ToDictionary(x => x.FeatureName, StringComparer.OrdinalIgnoreCase);

        foreach (var toggle in toggles)
        {
            if (string.IsNullOrWhiteSpace(toggle.FeatureName))
            {
                continue;
            }

            if (existingMap.TryGetValue(toggle.FeatureName, out var existingFeature))
            {
                existingFeature.IsEnabled = toggle.IsEnabled;
                existingFeature.Reason = toggle.Reason;
                existingFeature.UpdatedAt = now;
                existingFeature.UpdatedBy = userId;
            }
            else
            {
                var tenantFeature = new TenantFeature
                {
                    TenantId = tenantId,
                    FeatureName = toggle.FeatureName,
                    IsEnabled = toggle.IsEnabled,
                    Reason = toggle.Reason,
                    CreatedAt = now,
                    CreatedBy = userId
                };

                _dbContext.TenantFeatures.Add(tenantFeature);
                existingMap[toggle.FeatureName] = tenantFeature;
            }
        }

        _tenantContext.TenantId = tenantId;
        _tenantContext.ResolutionSource = "AdminTenantAction";

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantFeaturesUpdated,
            "TenantFeatures",
            tenantId,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            System.Text.Json.JsonSerializer.Serialize(toggles),
            cancellationToken);

        var states = existingMap.Values
            .OrderBy(feature => feature.FeatureName)
            .Select(feature => new TenantFeatureState(
                feature.FeatureName,
                feature.IsEnabled,
                feature.UpdatedAt))
            .ToList();

        return new TenantFeatureList(tenantId, states);
    }

    private async Task EnsureTenantExistsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found");
        }
    }

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }
}
