using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Authorization;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Convention: Permission keys contain '.' (e.g., "Invoice.Create")
        // Skip FastEndpoints internal policies (e.g., "epPolicy:Namespace.Endpoint")
        if (policyName.Contains('.') && !policyName.StartsWith("epPolicy:", StringComparison.OrdinalIgnoreCase))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }


        // Fall back to default provider for named policies
        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
