namespace Aonik.SharedKernel.Security;

/// <summary>
/// Fail-closed startup guard for hosts that wire development-only, blanket-trust
/// stand-ins for security services — e.g. a grant-all <c>IPermissionService</c>, an
/// auto-clear <c>IComplianceService</c>, or a fixed "always PlatformAdmin" identity.
/// Such hosts (the Finance and Platform MCP servers) must never run outside the
/// Development environment, where those stubs would silently bypass authorization or
/// compliance screening (backend review finding C4).
///
/// Call <see cref="EnsureDevelopmentOnly"/> at host startup, before building the host,
/// so an insecure process refuses to start anywhere but Development rather than coming up
/// and serving an agent blanket authority.
/// </summary>
public static class DevelopmentOnlyHostGuard
{
    /// <summary>The only environment name in which a guarded host is permitted to run.</summary>
    public const string DevelopmentEnvironmentName = "Development";

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> unless <paramref name="environmentName"/>
    /// is "Development" (case-insensitive). The generic host defaults an unset
    /// <c>DOTNET_ENVIRONMENT</c> to "Production", so this fails closed by default: a guarded
    /// host only starts when something has explicitly designated Development.
    /// </summary>
    /// <param name="environmentName">The resolved host environment name (e.g. <c>builder.Environment.EnvironmentName</c>).</param>
    /// <param name="hostDescription">Human label for the host, woven into the failure message (e.g. "The Finance MCP server").</param>
    /// <param name="reason">Why the host is development-only, woven into the failure message (e.g. which insecure stubs it registers).</param>
    public static void EnsureDevelopmentOnly(string? environmentName, string hostDescription, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (string.Equals(environmentName, DevelopmentEnvironmentName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{hostDescription} is restricted to the Development environment, but the current environment is " +
            $"'{(string.IsNullOrWhiteSpace(environmentName) ? "(unset → Production)" : environmentName)}'. {reason} " +
            "Refusing to start so these development-only stubs can never run outside Development. " +
            $"To run it locally, set DOTNET_ENVIRONMENT={DevelopmentEnvironmentName}; to run it elsewhere, " +
            "replace the stubs with real security service implementations first.");
    }
}
