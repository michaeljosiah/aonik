using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A tenant-declared HTTP / OpenAPI tool (Spec 033 §8.4): one external REST call — method, URL
/// template, a declared JSON parameter schema, and an auth reference — exposed as a single
/// <c>AIFunction</c>. No server to run, no code to upload. Because the parameter surface is the
/// declared schema, the model cannot smuggle arbitrary fields into the call.
/// <para>
/// Defaults to <see cref="TenantToolRiskTier.High"/> for any method that is not a plainly
/// read-only GET; a PlatformAdmin may reclassify a side-effect-free GET to
/// <see cref="TenantToolRiskTier.ReadOnly"/>. A tenant cannot lower the tier.
/// </para>
/// </summary>
public class TenantHttpTool : AuditableEntity, ITenantScoped
{
    /// <summary>The owning tenant. Enforced by the module tenant query filter.</summary>
    public Guid TenantId { get; set; }

    /// <summary>The tool name the model sees; unique per tenant. Must not collide with a built-in tool name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human description shown to the model and in the UI.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>HTTP method (GET, POST, PUT, PATCH, DELETE).</summary>
    public string Method { get; set; } = "GET";

    /// <summary>URL template with typed placeholders (e.g. <c>https://api.example.com/v1/orders/{id}</c>). Host must be on the egress allow-list.</summary>
    public string UrlTemplate { get; set; } = string.Empty;

    /// <summary>The declared JSON Schema for the tool's parameters — the fixed surface the model may fill.</summary>
    public string ParameterSchemaJson { get; set; } = "{}";

    /// <summary>How the call authenticates.</summary>
    public TenantToolAuthKind AuthKind { get; set; } = TenantToolAuthKind.None;

    /// <summary>
    /// <c>ISettingValueProtector</c>-encrypted JSON holding the auth secret(s). Decrypted only
    /// server-side at call time; never returned to any client.
    /// </summary>
    public string? ProtectedAuthJson { get; set; }

    /// <summary>
    /// Risk classification. Defaults to <see cref="TenantToolRiskTier.High"/> for non-GET; a
    /// PlatformAdmin may set <see cref="TenantToolRiskTier.ReadOnly"/> (side-effect-free GET) or a
    /// lower mutating tier. A tenant cannot lower it.
    /// </summary>
    public TenantToolRiskTier RiskTier { get; set; } = TenantToolRiskTier.High;

    /// <summary>Short human label for the action, used in audit + approval messages.</summary>
    public string ActionKind { get; set; } = string.Empty;

    /// <summary>For a High tool, the <c>Proposal.ProposalType</c> the mutation marshals into.</summary>
    public string? ProposalType { get; set; }

    /// <summary>Review lifecycle state (Spec 033 §7.1). Always requires explicit platform review.</summary>
    public TenantExtensionApprovalState ApprovalState { get; set; } = TenantExtensionApprovalState.Draft;

    /// <summary>The PlatformAdmin who last reviewed this tool (validates egress host + tier).</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>When the last platform review decision was recorded.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reviewer note or rejection reason, surfaced to the tenant.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>Whether the tenant has activated the tool (only meaningful once Approved).</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Monotonic credential version, bumped whenever the auth secret is rotated. Lets cached
    /// declarative tools / clients be rebuilt on rotation (Spec 033 §8.3 / §8.4).
    /// </summary>
    public int CredentialVersion { get; set; } = 1;
}
