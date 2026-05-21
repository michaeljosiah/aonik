namespace Aonik.Infrastructure.Authentication.Provisioning;

/// <summary>
/// Spec 029 — small helpers shared across the six Keycloak* services. The
/// configured <c>Auth.Keycloak.Authority</c> is the full realm URL
/// (<c>{root}/realms/{realm}</c>). Token / userinfo endpoints hang off the
/// realm URL; the Admin REST API hangs off <em>root</em>. We never assemble
/// realm-specific paths by string concatenation outside this file.
/// </summary>
internal static class KeycloakUrls
{
    /// <summary>
    /// Trim trailing slashes and prepend <c>https://</c> if missing. Returns the
    /// realm URL (e.g. <c>https://keycloak.example.com/realms/aonik</c>).
    /// </summary>
    public static string NormalizeAuthority(string authority)
    {
        var trimmed = authority.Trim().TrimEnd('/');
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed}";
    }

    /// <summary>
    /// Given a realm URL <c>{root}/realms/{realm}</c>, return <c>{root}</c>.
    /// Used by Admin REST API calls which hang off <c>{root}/admin/realms/{realm}/...</c>.
    /// Falls back to the input unchanged when the URL doesn't contain a
    /// <c>/realms/</c> segment so a misconfigured authority surfaces as a clear
    /// 404 from the upstream rather than a silent rewrite here.
    /// </summary>
    public static string RealmRoot(string normalizedAuthority)
    {
        const string marker = "/realms/";
        var idx = normalizedAuthority.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? normalizedAuthority : normalizedAuthority[..idx];
    }
}
