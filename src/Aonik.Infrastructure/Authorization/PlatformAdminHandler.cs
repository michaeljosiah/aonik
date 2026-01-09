using Aonik.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Authorization;

public class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAdminHandler> _logger;
    
    public PlatformAdminHandler(
        IConfiguration configuration,
        ILogger<PlatformAdminHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        var options = _configuration.GetSection("PlatformAdmin").Get<PlatformAdminOptions>()
            ?? new PlatformAdminOptions();
        
        var principal = context.User;
        
        // Check for role claim (e.g., "roles": ["Aonik.PlatformAdmin"])
        var hasRoleClaim = principal.Claims.Any(c =>
            c.Type == options.RoleClaimType &&
            c.Value == options.RoleValue);
        
        // Check for scope claim (e.g., "aonik_platform_admin": "true")
        var hasScopeClaim = !string.IsNullOrEmpty(options.ScopeClaimType) &&
            principal.Claims.Any(c =>
                c.Type == options.ScopeClaimType &&
                (c.Value == "true" || c.Value == "1"));
        
        if (hasRoleClaim || hasScopeClaim)
        {
            _logger.LogInformation("Platform admin access granted");
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Platform admin access denied (missing required claims)");
        }
        
        return Task.CompletedTask;
    }
}
