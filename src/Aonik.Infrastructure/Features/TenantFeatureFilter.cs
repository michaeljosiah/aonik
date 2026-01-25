using Aonik.Application.Abstractions.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace Aonik.Infrastructure.Features;

[FilterAlias("Tenant")]
public class TenantFeatureFilter : IFeatureFilter
{
    private readonly ITenantProvider _tenantProvider;

    public TenantFeatureFilter(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            return Task.FromResult(false);
        }

        var settings = context.Parameters.Get<TenantFeatureFilterSettings>()
            ?? new TenantFeatureFilterSettings();

        if (settings.AllowedTenants.Count == 0)
        {
            return Task.FromResult(false);
        }

        if (settings.AllowedTenants.Contains("*"))
        {
            return Task.FromResult(true);
        }

        var tenantIdValue = tenantId.ToString();
        var isEnabled = settings.AllowedTenants.Contains(tenantIdValue);
        return Task.FromResult(isEnabled);
    }
}

public class TenantFeatureFilterSettings
{
    public List<string> AllowedTenants { get; set; } = new();
}
