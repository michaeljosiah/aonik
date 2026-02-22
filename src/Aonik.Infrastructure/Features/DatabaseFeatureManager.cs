using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;

namespace Aonik.Infrastructure.Features;

public class DatabaseFeatureManager : IFeatureManager
{
    private readonly IFeatureManagerSnapshot _featureManagerSnapshot;
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public DatabaseFeatureManager(
        IFeatureManagerSnapshot featureManagerSnapshot,
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _featureManagerSnapshot = featureManagerSnapshot;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(string feature)
    {
        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            var tenantOverride = await GetTenantOverrideAsync(tenantId, feature);
            if (tenantOverride.HasValue)
            {
                return tenantOverride.Value;
            }
        }

        return await _featureManagerSnapshot.IsEnabledAsync(feature);
    }

    public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context)
    {
        return IsEnabledAsync(feature);
    }

    public IAsyncEnumerable<string> GetFeatureNamesAsync()
    {
        return _featureManagerSnapshot.GetFeatureNamesAsync();
    }

    private async Task<bool?> GetTenantOverrideAsync(Guid tenantId, string feature)
    {
        var now = _clock.UtcNow;

        var tenantFeature = await _dbContext.TenantFeatures
            .Where(x => x.TenantId == tenantId
                        && x.FeatureName == feature
                        && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync();

        return tenantFeature?.IsEnabled;
    }

}
