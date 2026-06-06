namespace Aonik.Agents.Entities;

/// <summary>
/// The risk classification persisted for a tenant-contributed tool (an HTTP tool, or the default
/// for a remote MCP server's discovered tools), per Spec 033 §8.5. This is the durable, DB-stored
/// form; the <c>TenantToolApprovalManifest</c> maps it onto the Spec 032
/// <c>ToolClassification</c> the gate consumes — <see cref="ReadOnly"/> becomes a pass-through and
/// the mutating tiers become a gated <c>ToolApprovalOptions</c>.
/// <para>
/// A tenant author cannot pick a tier below <see cref="High"/> for a mutating tool: mutating
/// tenant tools default to <see cref="High"/> (durable proposal) and only a PlatformAdmin review
/// may lower them to <see cref="Medium"/> / <see cref="Low"/> or classify a side-effect-free GET
/// as <see cref="ReadOnly"/> (Spec 033 §7, §8.4).
/// </para>
/// </summary>
public enum TenantToolRiskTier
{
    /// <summary>
    /// Safe read; the gate passes it through unchanged. Only a PlatformAdmin may assign this
    /// (e.g. a side-effect-free GET HTTP tool, or a read-looking MCP tool).
    /// </summary>
    ReadOnly,

    /// <summary>Reversible personal-state write; audited and run in-band (PlatformAdmin-lowered only).</summary>
    Low,

    /// <summary>Everyday domain write; requires an in-session confirmation (PlatformAdmin-lowered only).</summary>
    Medium,

    /// <summary>
    /// Default for any tenant mutating tool: marshalled into a durable proposal, never run in-band.
    /// </summary>
    High,
}
