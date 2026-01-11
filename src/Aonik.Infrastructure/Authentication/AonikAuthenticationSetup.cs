using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Services.Identity;
using Aonik.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Aonik.Infrastructure.Authentication;

public static class AonikAuthenticationSetup
{
    public static IServiceCollection AddAonikAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration.GetSection("Auth").Get<AuthOptions>() 
            ?? throw new InvalidOperationException("Auth configuration is missing");
        
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                ConfigureJwtBearerOptions(options, authOptions);
                ConfigureTokenValidationEvents(options);
            });
        
        return services;
    }
    
    private static void ConfigureJwtBearerOptions(JwtBearerOptions options, AuthOptions authOptions)
    {
        if (authOptions.Provider == "AzureAd")
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
        else if (authOptions.Provider == "Auth0")
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
        else
        {
            throw new InvalidOperationException($"Unsupported auth provider: {authOptions.Provider}");
        }
        
        options.RequireHttpsMetadata = true; // Always require HTTPS for metadata
    }
    
    private static void ConfigureTokenValidationEvents(JwtBearerOptions options)
    {
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                try
                {
                    await HandleTokenValidatedAsync(context);
                }
                catch (Exception ex)
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogError(ex, "Error during token validation");
                    context.Fail("Authentication failed");
                }
            },
            
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<JwtBearerEvents>>();
                
                // Log without exposing token details
                logger.LogWarning("Authentication failed for {Path}: {Error}",
                    context.HttpContext.Request.Path,
                    context.Exception.Message);
                
                return Task.CompletedTask;
            }
        };
    }
    
    private static async Task HandleTokenValidatedAsync(TokenValidatedContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<JwtBearerEvents>>();
        
        // 1. Extract token and claims
        // CRITICAL: Read from SecurityToken, not claims principal
        if (context.SecurityToken is not JwtSecurityToken jwtToken)
        {
            logger.LogError("SecurityToken is not a JwtSecurityToken");
            context.Fail("Invalid token type");
            return;
        }
        
        var iss = jwtToken.Issuer;
        
        // Prefer 'oid' (Entra) over 'sub' (Auth0/standard)
        var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
                  ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        
        var tid = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
        
        var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                    ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
        
        if (string.IsNullOrEmpty(iss) || string.IsNullOrEmpty(sub))
        {
            logger.LogWarning("Missing required claims: iss={Iss}, sub/oid={Sub}", iss, sub);
            context.Fail("Missing required claims");
            return;
        }
        
        // 2. Resolve tenant (fail fast if cannot resolve)
        // Store JWT token in HttpContext.Items for TenantResolver
        context.HttpContext.Items["JwtSecurityToken"] = jwtToken;
        
        var tenantResolver = context.HttpContext.RequestServices
            .GetRequiredService<ITenantResolver>();
        
        var aonikTenantId = await tenantResolver.ResolveTenantIdAsync(
            context.HttpContext.RequestAborted);
        
        if (aonikTenantId == null)
        {
            logger.LogWarning("Failed to resolve tenant for issuer {Issuer}, subject {Subject}", iss, sub);
            context.Fail("Tenant could not be resolved");
            return;
        }
        
        // 3. Resolve or create user (JIT provisioning)
        var userIdentityService = context.HttpContext.RequestServices
            .GetRequiredService<IUserIdentityService>();
        
        var user = await userIdentityService.ResolveOrCreateUserAsync(
            iss,
            sub,
            tid,
            email,
            aonikTenantId.Value,
            context.HttpContext.RequestAborted);
        
        // 4. Stash in HttpContext.Items for downstream consumers
        context.HttpContext.Items["AonikUserId"] = user.UserId;
        context.HttpContext.Items["AonikUserStatus"] = user.Status;
        
        logger.LogInformation("Authenticated user {UserId} in tenant {TenantId} (Status: {Status})",
            user.UserId, aonikTenantId.Value, user.Status);
        
        // Check if user is suspended/deactivated
        if (user.Status != "Active")
        {
            logger.LogWarning("User {UserId} attempted login with status {Status}", user.UserId, user.Status);
            context.Fail($"User account is {user.Status}");
        }
    }
}
