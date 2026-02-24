using System.IdentityModel.Tokens.Jwt;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Aonik.Platform.Contracts.Models.Configuration;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure.Authentication.Configuration;

namespace Aonik.Infrastructure.Authentication;

public class TenantResolver : ITenantResolver
{
    private readonly IAonikDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantResolver> _logger;

    public TenantResolver(
        IAonikDbContext dbContext,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TenantResolver> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Guid? ResolveTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null in TenantResolver");
            return null;
        }

        var mode = _configuration.GetValue<TenantRoutingMode>("Auth:TenantRouting");

        return mode switch
        {
            TenantRoutingMode.Claim => ResolveFromClaim(httpContext),
            TenantRoutingMode.Subdomain => ResolveFromSubdomainAsync(httpContext).GetAwaiter().GetResult(),
            TenantRoutingMode.Header => ResolveFromHeader(httpContext),
            _ => null
        };
    }

    public Guid? ResolveFromHttpContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var mode = _configuration.GetValue<TenantRoutingMode>("Auth:TenantRouting");

        return mode switch
        {
            TenantRoutingMode.Header => ResolveFromHeader(httpContext),
            TenantRoutingMode.Subdomain => ResolveFromSubdomainAsync(httpContext).GetAwaiter().GetResult(),
            _ => null
        };
    }

    private Guid? ResolveFromClaim(HttpContext httpContext)
    {
        // Prefer claims from authenticated principal (works for JwtSecurityToken and JsonWebToken)
        var tenantClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == "aonik_tenant_id")?.Value;

        // Back-compat: tests and some middleware stash JwtSecurityToken in Items
        if (string.IsNullOrEmpty(tenantClaim)
            && httpContext.Items["JwtSecurityToken"] is JwtSecurityToken jwtToken)
        {
            tenantClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "aonik_tenant_id")?.Value;
        }

        if (string.IsNullOrEmpty(tenantClaim))
        {
            _logger.LogDebug("No aonik_tenant_id claim found");
            return null;
        }

        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            _logger.LogWarning("Invalid aonik_tenant_id claim format: {Value}", tenantClaim);
            return null;
        }

        return tenantId;
    }

    private async Task<Guid?> ResolveFromSubdomainAsync(HttpContext httpContext)
    {
        var host = httpContext.Request.Host.Host;
        var parts = host.Split('.');

        if (parts.Length < 3)
        {
            _logger.LogDebug("Host does not contain subdomain: {Host}", host);
            return null;
        }

        var subdomain = parts[0];

        var tenant = await _dbContext.Tenants
            .Where(t => t.Subdomain == subdomain && t.Status == "Active")
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (tenant == null)
        {
            _logger.LogDebug("No active tenant found for subdomain: {Subdomain}", subdomain);
            return null;
        }

        return tenant.Id;
    }

    private Guid? ResolveFromHeader(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(header))
        {
            _logger.LogDebug("No X-Tenant-Id header found");
            return null;
        }

        if (!Guid.TryParse(header, out var tenantId))
        {
            _logger.LogWarning("Invalid X-Tenant-Id header format: {Value}", header);
            return null;
        }

        return tenantId;
    }
}
