using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

public class Agent : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public Guid InstructionPromptSpecId { get; set; }
    public string ToolsetIdsJson { get; set; } = string.Empty;
    public string InputSchemaJson { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public string PermissionsProfileJson { get; set; } = string.Empty;
    public string RiskTier { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
