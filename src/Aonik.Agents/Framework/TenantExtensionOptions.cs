namespace Aonik.Agents.Framework;

/// <summary>
/// Platform-curated options governing tenant-managed agent extensions (Spec 033). Bound from the
/// <c>Agents:TenantExtensions</c> configuration section. The egress allow-list is the network half
/// of the spec's "ask before" guardrail (§6): a tenant MCP/HTTP destination must be on it, checked
/// at PlatformAdmin approval and re-checked at connect/call to prevent SSRF-style use of a tenant
/// tool to reach internal services.
/// </summary>
public sealed class TenantExtensionOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Agents:TenantExtensions";

    /// <summary>
    /// Allowed egress hosts for tenant MCP servers and HTTP tools. Each entry is either an exact
    /// host (e.g. <c>api.example.com</c>) or a wildcard suffix (<c>*.example.com</c>, which matches
    /// any sub-domain but not the apex). Matching is case-insensitive.
    /// </summary>
    public List<string> AllowedEgressHosts { get; set; } = new();

    /// <summary>
    /// When true, any host is permitted (egress allow-list disabled). Intended ONLY for local
    /// development; defaults to false so production fails closed on an unlisted host.
    /// </summary>
    public bool AllowAnyEgressHost { get; set; }

    /// <summary>
    /// When true, plain <c>http://</c> egress is permitted. Defaults to false — tenant destinations
    /// must be <c>https://</c> so credentials are not sent in the clear.
    /// </summary>
    public bool AllowInsecureEgress { get; set; }

    /// <summary>
    /// Whether tenant skill <c>scripts/</c> execution is permitted at all on this deployment. Even
    /// when true, a skill's scripts stay off until a PlatformAdmin enables them per skill, and the
    /// framework <c>ScriptApproval</c> hook remains on (Spec 033 §8.2). Defaults to false.
    /// </summary>
    public bool AllowSkillScripts { get; set; }
}
