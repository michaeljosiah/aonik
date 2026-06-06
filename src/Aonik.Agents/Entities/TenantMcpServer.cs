using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A tenant-registered remote MCP server (Spec 033 §8.3) whose tools become callable by the
/// tenant's agents. Unlike the first-party <c>Agents:McpServers</c> stdio path, this is
/// <strong>scoped</strong> (per tenant), <strong>persisted</strong> (a DB row, not config), and
/// <strong>remote</strong> (HTTP/SSE via <c>HttpClientTransport</c> — never a spawned process).
/// <para>
/// Discovered tools are routed through the Spec 032 approval gate exactly like built-ins; a
/// mutating one defaults to <see cref="TenantToolRiskTier.High"/> (durable proposal). The server's
/// <see cref="Endpoint"/> host must be on the platform egress allow-list, checked at PlatformAdmin
/// approval and re-checked at connect.
/// </para>
/// </summary>
public class TenantMcpServer : AuditableEntity, ITenantScoped
{
    /// <summary>The owning tenant. Enforced by the module tenant query filter.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Operator-chosen display name; unique per tenant. Also used as the MCP transport name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The remote MCP endpoint URL. Its host must be on the platform egress allow-list.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Remote transport mode (HTTP or SSE). Never stdio for tenant servers.</summary>
    public TenantMcpTransportType TransportType { get; set; } = TenantMcpTransportType.Http;

    /// <summary>How the server authenticates.</summary>
    public TenantToolAuthKind AuthKind { get; set; } = TenantToolAuthKind.None;

    /// <summary>
    /// <c>ISettingValueProtector</c>-encrypted JSON holding the auth secret(s). Decrypted only
    /// server-side at connect time; never returned to any client.
    /// </summary>
    public string? ProtectedAuthJson { get; set; }

    /// <summary>
    /// Optional JSON array of tool-name prefixes the tenant wants exposed from this server; when
    /// non-empty, discovered tools whose names don't match a prefix are dropped.
    /// </summary>
    public string AllowedToolPrefixesJson { get; set; } = "[]";

    /// <summary>
    /// The tier assigned to this server's <em>mutating</em> discovered tools. Defaults to
    /// <see cref="TenantToolRiskTier.High"/>; only a PlatformAdmin review may lower it. Read-looking
    /// discovered tools are classified <see cref="TenantToolRiskTier.ReadOnly"/> regardless.
    /// </summary>
    public TenantToolRiskTier DefaultRiskTier { get; set; } = TenantToolRiskTier.High;

    /// <summary>Review lifecycle state (Spec 033 §7.1). Always requires explicit platform review.</summary>
    public TenantExtensionApprovalState ApprovalState { get; set; } = TenantExtensionApprovalState.Draft;

    /// <summary>The PlatformAdmin who last reviewed this server (validates egress host + default tier).</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>When the last platform review decision was recorded.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reviewer note or rejection reason, surfaced to the tenant.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>Whether the tenant has activated the server (only meaningful once Approved).</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Monotonic credential version, bumped whenever the auth secret is rotated or the connection
    /// inputs change. Keys the per-tenant MCP connection cache so a rotation invalidates it
    /// (Spec 033 §8.3).
    /// </summary>
    public int CredentialVersion { get; set; } = 1;
}
