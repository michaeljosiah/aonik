using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


using Aonik.SharedKernel.Abstractions;

namespace Aonik.Api.Tests;


public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string TenantIdHeader = "X-Test-Tenant-Id";
    public const string RolesHeader = "X-Test-Roles";
    public const string ClaimsHeader = "X-Test-Claims";

    private readonly ICurrentUserContext _currentUserContext;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICurrentUserContext currentUserContext)
        : base(options, logger, encoder)
    {
        _currentUserContext = currentUserContext;
    }


    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValue)
            || !Guid.TryParse(userIdValue, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new("iss", "test")
        };

        Guid? tenantId = null;
        if (Request.Headers.TryGetValue(TenantIdHeader, out var tenantIdValue)
            && Guid.TryParse(tenantIdValue, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
            claims.Add(new Claim("aonik_tenant_id", parsedTenantId.ToString()));
        }

        var roles = new List<string>();
        if (Request.Headers.TryGetValue(RolesHeader, out var rolesValue))
        {
            roles = rolesValue
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            foreach (var role in roles)
            {
                claims.Add(new Claim("roles", role));
            }
        }

        if (Request.Headers.TryGetValue(ClaimsHeader, out var claimsValue))
        {
            claims.AddRange(ParseClaims(claimsValue));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        var token = new JwtSecurityToken(issuer: "test", claims: claims);
        Context.Items["JwtSecurityToken"] = token;

        Context.Items["AonikUserId"] = userId;
        if (tenantId.HasValue)
        {
            Context.Items["AonikTenantId"] = tenantId.Value;
        }

        if (roles.Count > 0)
        {
            Context.Items["AonikRoles"] = roles;
        }

        _currentUserContext.UserId = userId;
        _currentUserContext.TenantId = tenantId;
        _currentUserContext.Roles = roles;
        _currentUserContext.IsAuthenticated = true;

        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static IEnumerable<Claim> ParseClaims(string? rawClaims)
    {
        if (string.IsNullOrWhiteSpace(rawClaims))
        {
            return Enumerable.Empty<Claim>();
        }

        return rawClaims
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment =>
            {
                var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                return parts.Length == 2
                    ? new Claim(parts[0], parts[1])
                    : new Claim(parts[0], string.Empty);
            });
    }
}
