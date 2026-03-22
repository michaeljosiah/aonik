using Aonik.Infrastructure.Authentication.Configuration;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Options;
using NSwag;
using NSwag.Generation.Processors.Security;

namespace Aonik.Api.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddAonikSwagger(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration.GetSection("Auth").Get<AuthOptions>()
            ?? throw new InvalidOperationException("Auth configuration is missing");

        var swaggerOptions = configuration.GetSection("Swagger").Get<SwaggerOptions>()
            ?? new SwaggerOptions();

        services.SwaggerDocument(options =>
        {
            options.DocumentSettings = settings =>
            {
                settings.Title = "AONIK API";
                settings.Version = "v1";
                settings.Description = "AONIK Financial Platform API with multi-tenant authentication";

                // Add JWT Bearer security definition
                settings.AddSecurity("Bearer", new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.OAuth2,
                    Description = "OAuth2 authentication with JWT Bearer tokens",
                    Flow = OpenApiOAuth2Flow.Implicit,
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit = CreateOAuth2Flow(authOptions, swaggerOptions)
                    }
                });

                // Apply security requirement globally
                settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
            };
        });

        return services;
    }

    public static WebApplication UseAonikSwagger(
        this WebApplication app,
        IConfiguration configuration)
    {
        if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "dev")
        {
            var swaggerOptions = configuration.GetSection("Swagger").Get<SwaggerOptions>()
                ?? new SwaggerOptions();

            app.UseSwaggerGen(config =>
            {
                config.Path = "/swagger/{documentName}/swagger.json";

                // Configure OAuth2 settings for Swagger UI
                config.PostProcess = (document, request) =>
                {
                    // This ensures the document is accessible
                };
            });

            // Add custom configuration for OAuth2 in Swagger UI
            if (!string.IsNullOrEmpty(swaggerOptions.ClientId))
            {
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path.StartsWithSegments("/swagger"))
                    {
                        var html = context.Response.Body;
                        context.Response.Body = new MemoryStream();
                        await next();

                        if (context.Response.StatusCode == 200 &&
                            context.Response.ContentType?.Contains("text/html") == true)
                        {
                            context.Response.Body.Seek(0, SeekOrigin.Begin);
                            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

                            // Inject OAuth2 configuration
                            var oauth2Config = $@"
                                ui.initOAuth({{
                                    clientId: '{swaggerOptions.ClientId}',
                                    appName: 'AONIK API',
                                    scopeSeparator: ' ',
                                    scopes: '{string.Join(" ", swaggerOptions.Scopes)}',
                                    usePkceWithAuthorizationCodeGrant: true
                                }});";

                            body = body.Replace("</body>", $"<script>{oauth2Config}</script></body>");

                            context.Response.Body = html;
                            context.Response.ContentLength = body.Length;
                            await context.Response.WriteAsync(body);
                        }
                    }
                    else
                    {
                        await next();
                    }
                });
            }
        }

        return app;
    }

    private static OpenApiOAuthFlow CreateOAuth2Flow(AuthOptions authOptions, SwaggerOptions swaggerOptions)
    {
        string authorizationUrl;
        string tokenUrl;

        if (authOptions.Provider == "AzureAd")
        {
            // Azure AD OAuth2 endpoints
            var authority = authOptions.AzureAd.Authority;
            authorizationUrl = $"{authority}/authorize";
            tokenUrl = $"{authority}/token";
        }
        else if (authOptions.Provider == "Auth0")
        {
            // Auth0 OAuth2 endpoints
            var authority = authOptions.Auth0.Authority.TrimEnd('/');
            authorizationUrl = $"{authority}/authorize";
            tokenUrl = $"{authority}/oauth/token";
        }
        else
        {
            throw new InvalidOperationException($"Unsupported auth provider: {authOptions.Provider}");
        }

        var flow = new OpenApiOAuthFlow
        {
            AuthorizationUrl = authorizationUrl,
            TokenUrl = tokenUrl,
            Scopes = new Dictionary<string, string>()
        };

        // Add configured scopes
        foreach (var scope in swaggerOptions.Scopes)
        {
            flow.Scopes[scope] = $"Access to {scope}";
        }

        return flow;
    }
}

public class SwaggerOptions
{
    /// <summary>
    /// OAuth2 Client ID for Swagger UI
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 scopes required for API access
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Redirect URI for OAuth2 callback (typically /swagger/oauth2-redirect.html)
    /// </summary>
    public string RedirectUri { get; set; } = "/swagger/oauth2-redirect.html";
}
