using Aonik.Platform.Contracts.Models.Configuration;
using Aonik.Platform.Contracts.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

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

        // Check for admin email match (bootstrap-only)
        var hasAdminEmail = false;
        if (options.AdminEmails.Length > 0)
        {
            var userEmail = ClaimsEmailResolver.GetEmail(principal);

            if (!string.IsNullOrEmpty(userEmail))
            {
                hasAdminEmail = options.AdminEmails.Any(adminEmail =>
                    string.Equals(adminEmail, userEmail, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (hasRoleClaim || hasAdminEmail)
        {
            _logger.LogInformation("Platform admin access granted (role={HasRole}, email={HasEmail})",
                hasRoleClaim, hasAdminEmail);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Platform admin access denied (role={HasRole}, email={HasEmail}). Claims: {Claims}",
                hasRoleClaim,
                hasAdminEmail,
                string.Join(", ", principal.Claims.Select(c => $"{c.Type}={c.Value}")));
        }


        return Task.CompletedTask;
    }
}
