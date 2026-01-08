using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiRoutePolicy : AuditableEntity
{
    public Guid AiRoutePolicyId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string UseCase { get; private set; } = string.Empty;
    public string RiskTier { get; private set; } = string.Empty;
    public string DataSensitivity { get; private set; } = string.Empty;
    public decimal CostCeiling { get; private set; }
    public Guid PrimaryModelId { get; private set; }
    public string FallbackModelIdsJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private AiRoutePolicy() { }

    public AiRoutePolicy(string useCase, string riskTier, string dataSensitivity, decimal costCeiling, Guid primaryModelId, Guid? tenantId = null)
    {
        AiRoutePolicyId = Id;
        TenantId = tenantId;
        UseCase = useCase;
        RiskTier = riskTier;
        DataSensitivity = dataSensitivity;
        CostCeiling = costCeiling;
        PrimaryModelId = primaryModelId;
        FallbackModelIdsJson = "[]";
        IsActive = true;
    }

    public void UpdatePrimaryModel(Guid primaryModelId)
    {
        PrimaryModelId = primaryModelId;
    }

    public void UpdateFallbackModels(string fallbackModelIdsJson)
    {
        FallbackModelIdsJson = fallbackModelIdsJson;
    }

    public void UpdateCostCeiling(decimal costCeiling)
    {
        CostCeiling = costCeiling;
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
