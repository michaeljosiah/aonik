using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Features;
using Aonik.Platform.Services;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Features;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Features;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Features;

internal class TenantFeatureService : AdminServiceBase, ITenantFeatureService
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlationContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ITenantContext _tenantContext;

    public TenantFeatureService(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IAuditLogWriter auditLogWriter,
        IPermissionService permissionService,
        ITenantContext tenantContext)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _correlationContext = correlationContext;
        _auditLogWriter = auditLogWriter;
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
        var userId = CurrentUserProvider.GetCurrentUserId();
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

}
