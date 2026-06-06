namespace Aonik.Agents.Entities;

/// <summary>
/// Remote transport a tenant MCP server is reached over. Tenant servers are <strong>remote
/// only</strong> (Spec 033 §5.2): there is deliberately no stdio option, so a tenant can never
/// cause a local child process to spawn inside the API host. Both modes are carried by the
/// framework's <c>HttpClientTransport</c>.
/// </summary>
public enum TenantMcpTransportType
{
    /// <summary>Streamable HTTP transport (the modern MCP HTTP transport).</summary>
    Http,

    /// <summary>Server-Sent Events transport (legacy HTTP/SSE servers).</summary>
    Sse,
}
