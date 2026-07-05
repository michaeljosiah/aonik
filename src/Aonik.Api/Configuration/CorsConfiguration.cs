namespace Aonik.Api.Configuration;

/// <summary>
/// CORS service registration and pipeline wiring for the Aonik API.
/// </summary>
/// <remarks>
/// Extracted out of <c>Program.cs</c> as part of decomposing the
/// composition root into focused extensions. The Aonik API uses a single
/// named CORS policy (<see cref="PolicyName"/>) that:
/// <list type="bullet">
///   <item>Reads allowed origins from <c>Cors:AllowedOrigins</c> configuration.</item>
///   <item>Always allows the local-dev front-ends (5173, 5174, 4201) so a
///         developer doesn't need to populate appsettings to run locally.</item>
///   <item>Allows credentials so cookie-based / Authorization-bearing
///         requests work cross-origin.</item>
/// </list>
///
/// The policy is enforced two ways:
/// <list type="number">
///   <item>App-wide, via <c>app.UseCors(PolicyName)</c> placed before
///         <c>UseHttpsRedirection</c> so OPTIONS preflights are handled
///         and short-circuited cleanly without bouncing through a 307.</item>
///   <item>Per-endpoint, via FastEndpoints' <c>RequireCors</c> metadata,
///         which the framework attaches to every endpoint via the
///         <c>Endpoints.Configurator</c> hook in <c>UseFastEndpoints</c>.</item>
/// </list>
///
/// This replaced a previous 147-line custom middleware that hand-rolled
/// preflight handling and OnStarting header attachment. Modern ASP.NET
/// Core <c>UseCors</c> (with <c>RequireCors</c> endpoint metadata) covers
/// both cases natively when ordered correctly.
/// </remarks>
public static class CorsConfiguration
{
    /// <summary>The single CORS policy name applied across the API.</summary>
    public const string PolicyName = "AonikCors";

    /// <summary>
    /// Always-allowed origins for local development. Kept hard-coded so
    /// running the API locally without populating appsettings still works.
    /// </summary>
    private static readonly string[] LocalDevOrigins =
    [
        "http://localhost:5173",   // Aonik.AdminUi (Vite)
        "http://localhost:5174",   // Payabo (Vite)
        "http://localhost:4201",   // legacy Angular dev port
        "http://127.0.0.1:4201",
    ];

    /// <summary>
    /// The literal <c>"null"</c> origin, opt-in via <c>Cors:AllowDesktopNullOrigin</c>.
    /// <para>
    /// The Aonik Admin desktop (Electron) renderer loads from <c>file://</c>, and
    /// Chromium sends <c>Origin: null</c> for its cross-origin fetches, so serving
    /// that client requires allowlisting the literal string <c>"null"</c>.
    /// </para>
    /// <para>
    /// But <c>Origin: null</c> is ALSO produced by sandboxed iframes and some
    /// redirects, so allowing it together with <see cref="CorsPolicyBuilder.AllowCredentials"/>
    /// lets credentialed cross-origin requests come from contexts we don't trust —
    /// the same-origin protection CORS exists to provide is weakened (M12). It is
    /// therefore <b>off by default</b> and only enabled for deployments that ship
    /// the desktop, via <c>Cors:AllowDesktopNullOrigin=true</c>. The proper
    /// long-term fix is to load the desktop from a real origin (a custom
    /// <c>app://</c> protocol or a loopback server) and allowlist that instead.
    /// </para>
    /// </summary>
    private static readonly string[] DesktopNullOrigin =
    [
        "null",
    ];

    /// <summary>
    /// Registers the <c>AonikCors</c> policy in DI. The configured origins are
    /// merged with <see cref="LocalDevOrigins"/> (and, only when
    /// <c>Cors:AllowDesktopNullOrigin</c> is set, <see cref="DesktopNullOrigin"/>)
    /// and de-duplicated.
    /// </summary>
    public static IServiceCollection AddAonikCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        // Off by default: allowing "null" alongside credentials is a spoofing
        // avenue (see DesktopNullOrigin). Deployments that ship the Electron
        // desktop opt in explicitly.
        var allowDesktopNullOrigin = configuration.GetValue("Cors:AllowDesktopNullOrigin", false);

        var allOrigins = configuredOrigins
            .Concat(LocalDevOrigins)
            .Concat(allowDesktopNullOrigin ? DesktopNullOrigin : [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct()
            .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(allOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// Wires the named CORS policy into the request pipeline.
    /// </summary>
    /// <remarks>
    /// MUST be called AFTER <c>UseRouting</c> (so endpoint-specific CORS
    /// metadata can be matched) but BEFORE the authentication / endpoint
    /// middleware so that 401/422/500 error responses still pick up the
    /// CORS headers via <c>OnStarting</c>. Modern ASP.NET Core handles
    /// OPTIONS preflight short-circuiting inside <c>UseCors</c> itself.
    /// </remarks>
    public static IApplicationBuilder UseAonikCors(this IApplicationBuilder app)
        => app.UseCors(PolicyName);
}
