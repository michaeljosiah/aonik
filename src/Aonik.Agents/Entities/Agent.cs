using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// Persisted agent configuration. Supports a two-level override model:
/// <list type="bullet">
///   <item><b>Global</b> (<c>TenantId = null</c>): Platform-wide defaults, seeded from
///     code-based <c>IDomainAgentDescriptor</c> registrations.</item>
///   <item><b>Tenant</b> (<c>TenantId = &lt;id&gt;</c>): Per-tenant overrides. A full copy
///     of the agent config that replaces the global row for that tenant.</item>
/// </list>
/// Resolution: tenant row wins over global row; if neither exists the code-based
/// descriptor is used as-is.
/// </summary>
public class Agent : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructionsText { get; set; } = string.Empty;
    public Guid InstructionPromptSpecId { get; set; }
    public string ToolsetIdsJson { get; set; } = string.Empty;
    public string InputSchemaJson { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public string PermissionsProfileJson { get; set; } = string.Empty;
    public string RiskTier { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
