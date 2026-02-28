using Aonik.Platform.Contracts.Models.Configuration;
using Aonik.Platform.Contracts.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Aonik.Infrastructure.Authorization;

public class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<PlatformAdminHandler> _logger;

    public PlatformAdminHandler(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<PlatformAdminHandler> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        var options = _configuration.GetSection("PlatformAdmin").Get<PlatformAdminOptions>()
            ?? new PlatformAdminOptions();

        var configuredAdminEmails = options.AdminEmails
            .Where(adminEmail => !string.IsNullOrWhiteSpace(adminEmail))
            .Select(adminEmail => adminEmail.Trim())
            .ToArray();

        var principal = context.User;

        // Check for role claim (e.g., "roles": ["Aonik.PlatformAdmin"])
        var hasRoleClaim = principal.Claims.Any(c =>
            c.Type == options.RoleClaimType &&
            c.Value == options.RoleValue);

        var hasScopeClaim = principal.Claims.Any(c =>
            c.Type == options.ScopeClaimType &&
            string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));

        // Check for admin email match (bootstrap-only)
        var hasAdminEmail = false;
        if (_environment.IsDevelopment() && configuredAdminEmails.Length > 0)
        {
            var userEmail = ClaimsEmailResolver.GetEmail(principal)?.Trim();

            if (!string.IsNullOrEmpty(userEmail))
            {
                hasAdminEmail = configuredAdminEmails.Any(adminEmail =>
                    string.Equals(adminEmail, userEmail, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (hasRoleClaim || hasScopeClaim || hasAdminEmail)
        {
            _logger.LogInformation("Platform admin access granted (role={HasRole}, scope={HasScope}, email={HasEmail})",
                hasRoleClaim,
                hasScopeClaim,
                hasAdminEmail);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Platform admin access denied (role={HasRole}, scope={HasScope}, email={HasEmail}). Claims: {Claims}",
                hasRoleClaim,
                hasScopeClaim,
                hasAdminEmail,
                string.Join(", ", principal.Claims.Select(c => $"{c.Type}={c.Value}")));
        }


        return Task.CompletedTask;
    }
}
