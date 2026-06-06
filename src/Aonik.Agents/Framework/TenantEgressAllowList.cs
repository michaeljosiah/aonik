using Microsoft.Extensions.Options;

namespace Aonik.Agents.Framework;

/// <summary>
/// Checks whether a tenant MCP/HTTP destination URL is permitted by the platform egress allow-list
/// (Spec 033 §6, §8.3, §11). Used at PlatformAdmin approval and re-checked at connect/call time so a
/// credential or endpoint that drifts off the list stops working immediately, and a tenant tool
/// cannot be pointed at an internal service (SSRF).
/// </summary>
public interface ITenantEgressAllowList
{
    /// <summary>
    /// True if <paramref name="url"/> is a well-formed absolute URL whose scheme and host are
    /// permitted. On false, <paramref name="reason"/> explains why (for surfacing to the operator).
    /// </summary>
    bool IsAllowed(string? url, out string? reason);
}

/// <summary>Default <see cref="ITenantEgressAllowList"/> backed by <see cref="TenantExtensionOptions"/>.</summary>
internal sealed class TenantEgressAllowList : ITenantEgressAllowList
{
    private readonly IOptionsMonitor<TenantExtensionOptions> _options;

    public TenantEgressAllowList(IOptionsMonitor<TenantExtensionOptions> options)
    {
        _options = options;
    }

    public bool IsAllowed(string? url, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "The endpoint URL is not a valid absolute URL.";
            return false;
        }

        var options = _options.CurrentValue;

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (!isHttps && !(isHttp && options.AllowInsecureEgress))
        {
            reason = options.AllowInsecureEgress
                ? "The endpoint must use http or https."
                : "The endpoint must use https.";
            return false;
        }

        if (options.AllowAnyEgressHost)
        {
            reason = null;
            return true;
        }

        var host = uri.Host;
        foreach (var allowed in options.AllowedEgressHosts)
        {
            if (HostMatches(host, allowed))
            {
                reason = null;
                return true;
            }
        }

        reason = $"Host '{host}' is not on the platform egress allow-list.";
        return false;
    }

    private static bool HostMatches(string host, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        pattern = pattern.Trim();

        // Wildcard suffix: "*.example.com" matches any sub-domain, not the apex.
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = pattern[1..]; // ".example.com"
            return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && host.Length > suffix.Length;
        }

        return string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
