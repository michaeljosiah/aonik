using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;


using Aonik.Platform.Contracts.Models.Configuration;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Infrastructure.Authentication.Configuration;
using Aonik.Infrastructure.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Infrastructure.Authentication;

public static class AonikAuthenticationSetup
{
    public const string AuthFailureReasonItemKey = "AonikAuthFailureReason";
    private static readonly string[] AlternativeBearerHeaders =
    [
        "X-Authorization",
        "X-Forwarded-Authorization",
        "X-Original-Authorization"
    ];

    // WebSocket upgrades from Flutter's WebSocketChannel sometimes drop
    // Authorization headers — clients may pass the JWT as ?access_token=...
    // instead. Only honor the query token for paths where it's expected.
    // See spec docs/specifications/022.aonik-voice-realtime.md Phase 1.
    private static readonly string[] QueryStringTokenPaths =
    [
        "/ai/voice",
    ];

    private const string QueryStringTokenParameter = "access_token";

    public static IServiceCollection AddAonikAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration.GetSection("Auth").Get<AuthOptions>()
            ?? throw new InvalidOperationException("Auth configuration is missing");

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Aonik";
                options.DefaultChallengeScheme = "Aonik";
            })
            .AddPolicyScheme("Aonik", "Aonik", options =>
            {
                options.ForwardDefaultSelector = context => SelectScheme(context, authOptions);
            })
            .AddJwtBearer("AzureAd", options =>
            {
                ConfigureJwtBearerOptions(options, authOptions, "AzureAd");
                ConfigureTokenValidationEvents(options, authOptions);
            })
            .AddJwtBearer("Auth0", options =>
            {
                ConfigureJwtBearerOptions(options, authOptions, "Auth0");
                ConfigureTokenValidationEvents(options, authOptions);
            })
            // Spec 029 — third occupant of the existing multi-provider pattern.
            // Operator selects via Auth.Provider = "Keycloak"; SelectScheme below
            // routes incoming bearer tokens to this scheme when the issuer prefix
            // matches the configured realm authority.
            .AddJwtBearer("Keycloak", options =>
            {
                ConfigureJwtBearerOptions(options, authOptions, "Keycloak");
                ConfigureTokenValidationEvents(options, authOptions);
            });

        return services;
    }

    private static void ConfigureJwtBearerOptions(JwtBearerOptions options, AuthOptions authOptions, string provider)
    {
        if (provider == "AzureAd")
        {
            options.Authority = authOptions.AzureAd.Authority;
            options.Audience = authOptions.AzureAd.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = authOptions.AzureAd.ValidateIssuer,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(authOptions.AzureAd.ClockSkewSeconds),
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        }
        else if (provider == "Auth0")
        {
            options.Authority = authOptions.Auth0.Authority;
            options.Audience = authOptions.Auth0.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = authOptions.Auth0.ValidateIssuer,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(authOptions.Auth0.ClockSkewSeconds),
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        }
        else if (provider == "Keycloak")
        {
            options.Authority = authOptions.Keycloak.Authority;
            options.Audience = authOptions.Keycloak.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = authOptions.Keycloak.ValidateIssuer,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(authOptions.Keycloak.ClockSkewSeconds),
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
        }
        else
        {
            throw new InvalidOperationException($"Unsupported auth provider: {provider}");
        }

        options.RequireHttpsMetadata = true;
    }

    private static void ConfigureTokenValidationEvents(JwtBearerOptions options, AuthOptions authOptions)
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (!string.IsNullOrWhiteSpace(context.Token))
                {
                    return Task.CompletedTask;
                }

                var token = ResolveBearerToken(context.HttpContext.Request);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                try
                {
                    await HandleTokenValidatedAsync(context, authOptions);
                }
                catch (Exception ex)
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogError(ex, "Error during token validation");
                    context.HttpContext.Items[AuthFailureReasonItemKey] = ex.Message;
                    context.Fail("Authentication failed");
                }
            },

            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<JwtBearerEvents>>();

                logger.LogWarning("Authentication failed for {Path}: {Error}",
                    context.HttpContext.Request.Path,
                    context.Exception.Message);

                context.HttpContext.Items[AuthFailureReasonItemKey] = context.Exception.Message;

                return Task.CompletedTask;
            }
        };
    }

    private static async Task HandleTokenValidatedAsync(TokenValidatedContext context, AuthOptions authOptions)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<JwtBearerEvents>>();

        JwtSecurityToken? jwtToken = null;
        JsonWebToken? jsonToken = null;

        if (context.SecurityToken is JwtSecurityToken parsedJwt)
        {
            jwtToken = parsedJwt;
        }
        else if (context.SecurityToken is JsonWebToken parsedJson)
        {
            jsonToken = parsedJson;
        }
        else
        {
            logger.LogError("SecurityToken is not a JWT token type: {TokenType}", context.SecurityToken?.GetType().FullName);
            context.Fail("Invalid token type");
            return;
        }

        var iss = jwtToken?.Issuer ?? jsonToken?.Issuer;
        var claims = jwtToken?.Claims ?? jsonToken?.Claims ?? Array.Empty<Claim>();

        var sub = claims.FirstOrDefault(c => c.Type == "oid")?.Value
                  ?? claims.FirstOrDefault(c => c.Type == "sub")?.Value;

        var tid = claims.FirstOrDefault(c => c.Type == "tid")?.Value;

        var email = ClaimsEmailResolver.GetEmail(context.Principal);

        if (string.IsNullOrEmpty(iss) || string.IsNullOrEmpty(sub))
        {
            logger.LogWarning("Missing required claims: iss={Iss}, sub/oid={Sub}", iss, sub);
            context.HttpContext.Items[AuthFailureReasonItemKey] = "Missing required claims";
            context.Fail("Missing required claims");
            return;
        }

        if (await TryHandleBootstrapAsync(context, iss, sub))
        {
            return;
        }

        var activeProvider = await GetActiveProviderAsync(context, authOptions);
        var issuerProvider = GetProviderForIssuer(iss, authOptions);
        if (!string.Equals(activeProvider, issuerProvider, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Token issuer provider {IssuerProvider} does not match active provider {ActiveProvider}",
                issuerProvider, activeProvider);
            context.HttpContext.Items[AuthFailureReasonItemKey] = "Token issuer not allowed for active provider";
            context.Fail("Token issuer not allowed for active provider");
            return;
        }

        var tenantResolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<IAonikDbContext>();

        Guid? tenantId = null;

        tenantId = await tenantResolver.ResolveTenantIdAsync(context.HttpContext.RequestAborted);
        if (tenantId == null)
        {
            tenantId = await tenantResolver.ResolveFromHttpContextAsync(context.HttpContext.RequestAborted);
        }
        if (tenantId == null)
        {
            tenantId = await ResolveFromUserAssociationAsync(dbContext, iss, sub, context.HttpContext.RequestAborted);
        }

        if (tenantId == null)
        {
            logger.LogWarning("Failed to resolve tenant for user {Sub}", sub);
            context.HttpContext.Items[AuthFailureReasonItemKey] = "Tenant could not be resolved";
            context.Fail("Tenant could not be resolved");
            return;
        }

        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId.Value;
        tenantContext.ResolutionSource = "Authentication";

        var userIdentityService = context.HttpContext.RequestServices
            .GetRequiredService<IUserIdentityService>();

        var user = await userIdentityService.ResolveOrCreateUserAsync(
            iss,
            sub,
            tid,
            email,
            tenantId.Value,
            context.HttpContext.RequestAborted);

        var roles = new HashSet<string>(
            ClaimsRoleMapper.ExtractRoles(context.Principal),
            StringComparer.OrdinalIgnoreCase);

        var persistedRoles = await userIdentityService.GetRoleNamesAsync(user.Id, context.HttpContext.RequestAborted);
        foreach (var persistedRole in persistedRoles)
        {
            roles.Add(persistedRole);
        }

        var currentUserContext = context.HttpContext.RequestServices
            .GetRequiredService<ICurrentUserContext>();

        currentUserContext.UserId = user.Id;
        currentUserContext.TenantId = tenantId.Value;
        currentUserContext.ExternalIssuer = iss;
        currentUserContext.ExternalSubject = sub;
        currentUserContext.Roles = roles.Count == 0
            ? Array.Empty<string>()
            : roles.ToArray();
        currentUserContext.IsAuthenticated = context.Principal?.Identity?.IsAuthenticated == true;

        context.HttpContext.Items["AonikUserId"] = user.Id;
        context.HttpContext.Items["AonikUserStatus"] = user.Status;
        context.HttpContext.Items["AonikTenantId"] = tenantId.Value;

        logger.LogInformation("Authenticated user {UserId} in tenant {TenantId} (Status: {Status})",
            user.Id, tenantId.Value, user.Status);

        if (user.Status != "Active")
        {
            logger.LogWarning("User {UserId} attempted login with status {Status}", user.Id, user.Status);
            context.HttpContext.Items[AuthFailureReasonItemKey] = $"User account is {user.Status}";
            context.Fail($"User account is {user.Status}");
            return;
        }

        // Spec 026 Part 3 — token revocation. Compares the JWT's
        // iat ("issued at") claim against the most-recent revoke for
        // this user (FusionCache-backed; default 30 s TTL). Tokens
        // issued before the last revoke are rejected with 401; tokens
        // issued after a revoke are honoured (operator's "kill the
        // current sessions" intent, not a permanent ban — use
        // deactivate for ban semantics).
        var blocklist = context.HttpContext.RequestServices.GetRequiredService<IUserSessionBlocklist>();
        var tokenIssuedUtc = ResolveTokenIssuedAt(jwtToken, jsonToken, claims);
        if (await blocklist.IsRevokedAsync(tenantId.Value, user.Id, tokenIssuedUtc, context.HttpContext.RequestAborted))
        {
            logger.LogWarning(
                "User {UserId} attempted login with a revoked-session token (iat={IssuedAt})",
                user.Id,
                tokenIssuedUtc);
            context.HttpContext.Items[AuthFailureReasonItemKey] = "Sessions revoked";
            context.Fail("User session has been revoked");
            return;
        }

        // Intentionally not persisting last-login during token validation.
        // Token validation runs before tenant context middleware, and DB writes here can fail
        // for tenant-scoped entities.
    }

    /// <summary>
    /// Reads the JWT's <c>iat</c> ("issued at") claim and converts it
    /// to UTC. Used by the blocklist check (Spec 026 Part 3). Falls
    /// back to <c>nbf</c> when <c>iat</c> is missing, and to "now" as
    /// a last resort so a malformed token is treated as freshly issued
    /// (i.e. the blocklist won't trip on it — the regular validation
    /// pipeline will reject malformed claims separately).
    /// </summary>
    private static DateTime ResolveTokenIssuedAt(JwtSecurityToken? jwt, JsonWebToken? json, IEnumerable<Claim> claims)
    {
        if (jwt != null && jwt.IssuedAt != default)
        {
            return jwt.IssuedAt.ToUniversalTime();
        }

        if (json != null)
        {
            try { return json.IssuedAt.ToUniversalTime(); }
            catch { /* fall through */ }
        }

        var iatStr = claims.FirstOrDefault(c => c.Type == "iat")?.Value
                     ?? claims.FirstOrDefault(c => c.Type == "nbf")?.Value;
        if (!string.IsNullOrWhiteSpace(iatStr) && long.TryParse(iatStr, out var iatSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    private static async Task<Guid?> ResolveFromUserAssociationAsync(
        IAonikDbContext dbContext,
        string issuer,
        string subject,
        CancellationToken ct)
    {
        // Cross-tenant by necessity: this runs during token validation BEFORE any
        // tenant is resolved — it is the step that discovers WHICH tenant the user
        // belongs to, keyed by their global IdP identity (iss + sub). Users are
        // tenant-scoped, so under the fail-closed query filter (finding C5) a lookup
        // with no ambient tenant matches nothing, and every login that relies on
        // user-association tenant resolution (e.g. the /host/me/tenants bootstrap call)
        // would 401 with "Tenant could not be resolved". AcrossTenants() is the
        // sanctioned escape hatch; the iss + sub predicate keeps the read scoped to the
        // single authenticated user.
        var user = await dbContext.Users
            .AcrossTenants()
            .Where(u => u.ExternalIssuer == issuer && u.ExternalSubject == subject)
            .Select(u => new { u.TenantId })
            .FirstOrDefaultAsync(ct);

        if (user != null && user.TenantId != Guid.Empty)
        {
            return user.TenantId;
        }

        return null;
    }

    private static bool IsPlatformAdmin(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return false;
        }

        var platformAdminOptions = new PlatformAdminOptions();
        var roleClaim = principal.Claims.FirstOrDefault(c => c.Type == platformAdminOptions.RoleClaimType)?.Value;
        if (roleClaim == platformAdminOptions.RoleValue)
        {
            return true;
        }

        var userEmail = ClaimsEmailResolver.GetEmail(principal);
        if (!string.IsNullOrEmpty(userEmail) && platformAdminOptions.AdminEmails.Length > 0)
        {
            return platformAdminOptions.AdminEmails.Any(adminEmail =>
                string.Equals(adminEmail, userEmail, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static async Task<string> GetActiveProviderAsync(TokenValidatedContext context, AuthOptions authOptions)
    {
        var settingProvider = context.HttpContext.RequestServices.GetRequiredService<ISettingProvider>();
        var provider = await settingProvider.GetAsync(AuthSettingNames.Provider, context.HttpContext.RequestAborted);
        if (string.IsNullOrWhiteSpace(provider))
        {
            return authOptions.Provider;
        }

        var defaultProvider = SettingDefinitions.Get(AuthSettingNames.Provider)?.DefaultValue;
        if (!string.IsNullOrWhiteSpace(defaultProvider)
            && string.Equals(provider, defaultProvider, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(authOptions.Provider, defaultProvider, StringComparison.OrdinalIgnoreCase))
        {
            return authOptions.Provider;
        }

        return provider;
    }

    private static string GetProviderForIssuer(string issuer, AuthOptions authOptions)
    {
        if (!string.IsNullOrWhiteSpace(authOptions.AzureAd.Authority)
            && issuer.StartsWith(authOptions.AzureAd.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return "AzureAd";
        }

        if (!string.IsNullOrWhiteSpace(authOptions.Auth0.Authority)
            && issuer.StartsWith(authOptions.Auth0.Authority.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return "Auth0";
        }

        // Spec 029 — Keycloak realm issuer is the full realm URL
        // (e.g. https://keycloak.example.com/realms/aonik). Stable prefix
        // suitable for StartsWith routing; matches the Auth0 pattern (TrimEnd('/'))
        // for tolerance of trailing-slash variants.
        if (!string.IsNullOrWhiteSpace(authOptions.Keycloak.Authority)
            && issuer.StartsWith(authOptions.Keycloak.Authority.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            return "Keycloak";
        }

        return authOptions.Provider;
    }

    private static string? SelectScheme(HttpContext context, AuthOptions authOptions)
    {
        var token = GetBearerToken(context);
        if (string.IsNullOrWhiteSpace(token))
        {
            return authOptions.Provider;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var issuer = jwt.Issuer;
            return GetProviderForIssuer(issuer, authOptions);
        }
        catch
        {
            return authOptions.Provider;
        }
    }

    private static string? GetBearerToken(HttpContext context)
    {
        return ResolveBearerToken(context.Request);
    }

    /// <summary>
    /// Resolves a bearer token from the request. Tries headers first, then
    /// falls back to <c>?access_token=...</c> on paths in <see cref="QueryStringTokenPaths"/>
    /// (specifically WebSocket upgrade paths where Flutter's <c>WebSocketChannel</c>
    /// can't reliably attach an <c>Authorization</c> header).
    /// </summary>
    private static string? ResolveBearerToken(HttpRequest request)
    {
        var token = ResolveBearerTokenFromHeaders(request.Headers);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        // Query-string fallback for WebSocket upgrade paths only. Limiting the
        // path scope prevents accidentally accepting query tokens on regular
        // HTTP routes where they'd leak into server logs and referrer headers.
        if (!IsQueryStringTokenPath(request.Path))
        {
            return null;
        }

        var queryToken = request.Query[QueryStringTokenParameter].FirstOrDefault();
        return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken;
    }

    private static bool IsQueryStringTokenPath(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        foreach (var allowed in QueryStringTokenPaths)
        {
            if (path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveBearerTokenFromHeaders(IHeaderDictionary headers)
    {
        var authorization = headers["Authorization"].FirstOrDefault();
        var token = ExtractBearerToken(authorization);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        foreach (var headerName in AlternativeBearerHeaders)
        {
            token = ExtractBearerToken(headers[headerName].FirstOrDefault());
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private static string? ExtractBearerToken(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        if (headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return headerValue[bearerPrefix.Length..].Trim();
        }

        return null;
    }

    private static async Task<bool> TryHandleBootstrapAsync(
        TokenValidatedContext context,
        string issuer,
        string subject)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Path.StartsWithSegments("/bootstrap"))
        {
            return false;
        }

        var roles = ClaimsRoleMapper.ExtractRoles(context.Principal);
        var currentUserContext = httpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
        currentUserContext.ExternalIssuer = issuer;
        currentUserContext.ExternalSubject = subject;
        currentUserContext.Roles = roles;
        currentUserContext.IsAuthenticated = context.Principal?.Identity?.IsAuthenticated == true;
        return true;
    }
}
