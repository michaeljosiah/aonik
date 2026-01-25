using Aonik.Application.Abstractions.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Aonik.Infrastructure.Features;

[FilterAlias("Tenant")]
public class TenantFeatureFilter : IFeatureFilter
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantFeatureFilter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

        if (!tenantProvider.TryGetCurrentTenantId(out var tenantId))
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
