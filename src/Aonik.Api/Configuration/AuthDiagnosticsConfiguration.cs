namespace Aonik.Api.Configuration;

/// <summary>
/// Optional diagnostic middleware that logs which authorization-related
/// headers are present on selected request paths. Off by default; toggled
/// via <c>Auth:Diagnostics:LogHeaderPresence</c> in configuration.
/// </summary>
/// <remarks>
/// Used to debug edge / proxy / IdP auth-header drop-out scenarios where
/// the standard <c>Authorization</c> header gets renamed to
/// <c>X-Authorization</c> / <c>X-Forwarded-Authorization</c> /
/// <c>X-Original-Authorization</c> by an upstream component. Logs only
/// header presence, never values, so it's safe to leave on briefly in
/// non-prod when chasing a token issue.
/// </remarks>
public static class AuthDiagnosticsConfiguration
{
    private static readonly string[] InterestingPathPrefixes =
    [
        "/bootstrap",
        "/identity",
        "/host",
    ];

    /// <summary>
    /// Adds the auth-header presence logger to the pipeline. Caller is
    /// responsible for gating on the configuration toggle — typically:
    /// <code>
    /// if (config.GetValue&lt;bool&gt;("Auth:Diagnostics:LogHeaderPresence"))
    ///     app.UseAuthHeaderPresenceLogging();
    /// </code>
    /// </summary>
    public static IApplicationBuilder UseAuthHeaderPresenceLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var isInterestingPath = InterestingPathPrefixes
                .Any(prefix => path.StartsWithSegments(prefix));

            var hasAuthorization = context.Request.Headers.ContainsKey("Authorization");
            var hasXAuthorization = context.Request.Headers.ContainsKey("X-Authorization");
            var hasXForwardedAuthorization = context.Request.Headers.ContainsKey("X-Forwarded-Authorization");
            var hasXOriginalAuthorization = context.Request.Headers.ContainsKey("X-Original-Authorization");

            if (isInterestingPath
                || hasAuthorization
                || hasXAuthorization
                || hasXForwardedAuthorization
                || hasXOriginalAuthorization)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Aonik.AuthHeaderDiagnostics");

                logger.LogInformation(
                    "Request {Method} {Path} header presence: Authorization={HasAuthorization}, X-Authorization={HasXAuthorization}, X-Forwarded-Authorization={HasXForwardedAuthorization}, X-Original-Authorization={HasXOriginalAuthorization}, OriginPresent={HasOrigin}",
                    context.Request.Method,
                    path,
                    hasAuthorization,
                    hasXAuthorization,
                    hasXForwardedAuthorization,
                    hasXOriginalAuthorization,
                    context.Request.Headers.ContainsKey("Origin"));
            }

            await next();
        });
    }
}
