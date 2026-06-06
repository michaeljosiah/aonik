namespace Aonik.Agents.Entities;

/// <summary>
/// How a tenant-contributed remote tool (MCP server or HTTP tool) authenticates to its
/// destination. The concrete secret material is never stored on this enum; it lives encrypted in
/// the row's <c>ProtectedAuthJson</c> (<c>ISettingValueProtector</c>), is decrypted only
/// server-side at connect/call time, and is never returned to any client (Spec 033 §11).
/// </summary>
public enum TenantToolAuthKind
{
    /// <summary>No authentication; the destination is public or open within the egress allow-list.</summary>
    None,

    /// <summary>An <c>Authorization: Bearer &lt;token&gt;</c> header.</summary>
    BearerToken,

    /// <summary>A custom header carrying an API key (header name + value held in the protected blob).</summary>
    ApiKeyHeader,

    /// <summary>HTTP Basic authentication (username + password held in the protected blob).</summary>
    Basic,
}
