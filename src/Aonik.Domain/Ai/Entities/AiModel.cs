using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiModel : AuditableEntity
{
    public Guid AiModelId { get; private set; }
    public Guid AiProviderId { get; private set; }
    public string ModelName { get; private set; } = string.Empty;
    public int ContextWindow { get; private set; }
    public string CostProfileJson { get; private set; } = string.Empty;
    public string LatencyProfileJson { get; private set; } = string.Empty;
    public string PolicyTagsJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private AiModel() { }

    public AiModel(Guid aiProviderId, string modelName, int contextWindow)
    {
        AiModelId = Id;
        AiProviderId = aiProviderId;
        ModelName = modelName;
        ContextWindow = contextWindow;
        CostProfileJson = "{}";
        LatencyProfileJson = "{}";
        PolicyTagsJson = "[]";
        IsActive = true;
    }

    public void UpdateModelName(string modelName)
    {
        ModelName = modelName;
    }

    public void UpdateContextWindow(int contextWindow)
    {
        ContextWindow = contextWindow;
    }

    public void UpdateCostProfile(string costProfileJson)
    {
        CostProfileJson = costProfileJson;
    }

    public void UpdateLatencyProfile(string latencyProfileJson)
    {
        LatencyProfileJson = latencyProfileJson;
    }

    public void UpdatePolicyTags(string policyTagsJson)
    {
        PolicyTagsJson = policyTagsJson;
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
