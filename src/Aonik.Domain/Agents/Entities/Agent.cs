using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Agents.Entities;

public class Agent : AuditableEntity
{
    public Guid AgentId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public Guid InstructionPromptSpecId { get; private set; }
    public string ToolsetIdsJson { get; private set; } = string.Empty;
    public string InputSchemaJson { get; private set; } = string.Empty;
    public string OutputSchemaJson { get; private set; } = string.Empty;
    public string PermissionsProfileJson { get; private set; } = string.Empty;
    public string RiskTier { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Agent() { }

    public Agent(string name, string domain, Guid instructionPromptSpecId, string riskTier, Guid? tenantId = null)
    {
        AgentId = Id;
        TenantId = tenantId;
        Name = name;
        Domain = domain;
        InstructionPromptSpecId = instructionPromptSpecId;
        RiskTier = riskTier;
        ToolsetIdsJson = "[]";
        InputSchemaJson = "{}";
        OutputSchemaJson = "{}";
        PermissionsProfileJson = "{}";
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateInstructionPromptSpec(Guid instructionPromptSpecId)
    {
        InstructionPromptSpecId = instructionPromptSpecId;
    }

    public void UpdateToolset(string toolsetIdsJson)
    {
        ToolsetIdsJson = toolsetIdsJson;
    }

    public void UpdateSchemas(string inputSchemaJson, string outputSchemaJson)
    {
        InputSchemaJson = inputSchemaJson;
        OutputSchemaJson = outputSchemaJson;
    }

    public void UpdatePermissionsProfile(string permissionsProfileJson)
    {
        PermissionsProfileJson = permissionsProfileJson;
    }

    public void UpdateRiskTier(string riskTier)
    {
        RiskTier = riskTier;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
