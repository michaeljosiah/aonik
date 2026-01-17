using System.IdentityModel.Tokens.Jwt;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Aonik.Application.Abstractions.Authentication;
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

    public async Task<Guid?> ResolveTenantIdAsync(CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null in TenantResolver");
            return null;
        }

        // Get JWT token from HttpContext items (set by authentication middleware)
        if (httpContext.Items["JwtSecurityToken"] is not JwtSecurityToken jwtToken)
        {
            _logger.LogWarning("JwtSecurityToken not found in HttpContext.Items");
            return null;
        }

        var mode = _configuration.GetValue<TenantRoutingMode>("Auth:TenantRouting");

        return mode switch
        {
            TenantRoutingMode.Claim => ResolveFromClaim(jwtToken),
            TenantRoutingMode.Subdomain => await ResolveFromSubdomainAsync(httpContext, ct),
            TenantRoutingMode.Header => ResolveFromHeader(httpContext),
            _ => null
        };
    }

    private Guid? ResolveFromClaim(JwtSecurityToken jwtToken)
    {
        var tenantClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "aonik_tenant_id")?.Value;

        if (string.IsNullOrEmpty(tenantClaim))
        {
            _logger.LogWarning("Missing aonik_tenant_id claim in JWT");
            return null;
        }

        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            _logger.LogWarning("Invalid aonik_tenant_id claim format: {Value}", tenantClaim);
            return null;
        }

        return tenantId;
    }

    private async Task<Guid?> ResolveFromSubdomainAsync(HttpContext httpContext, CancellationToken ct)
    {
        // Extract subdomain from Host
        // IMPORTANT: Only use this if ForwardedHeadersOptions is properly configured
        var host = httpContext.Request.Host.Host;
        var parts = host.Split('.');

        if (parts.Length < 3)
        {
            _logger.LogWarning("Host does not contain subdomain: {Host}", host);
            return null;
        }

        var subdomain = parts[0];

        // Lookup tenant by subdomain
        var tenant = await _dbContext.Tenants
            .Where(t => t.Subdomain == subdomain && t.Status == "Active")
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(ct);

        if (tenant == null)
        {
            _logger.LogWarning("No active tenant found for subdomain: {Subdomain}", subdomain);
            return null;
        }

        return tenant.Id;

    }

    private Guid? ResolveFromHeader(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(header))
        {
            _logger.LogWarning("Missing X-Tenant-Id header");
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
