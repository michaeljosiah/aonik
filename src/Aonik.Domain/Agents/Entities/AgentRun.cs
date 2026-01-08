using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Agents.Entities;

public class AgentRun : AuditableEntity, ITenantScoped
{
    public Guid AgentRunId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AgentId { get; private set; }
    public string Goal { get; private set; } = string.Empty;
    public string PlanJson { get; private set; } = string.Empty;
    public string StepsJson { get; private set; } = string.Empty;
    public string LinkedAiRunIdsJson { get; private set; } = string.Empty;
    public string ArtifactsProducedJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;

    private AgentRun() { }

    public AgentRun(Guid tenantId, Guid agentId, string goal)
    {
        AgentRunId = Id;
        TenantId = tenantId;
        AgentId = agentId;
        Goal = goal;
        PlanJson = "{}";
        StepsJson = "[]";
        LinkedAiRunIdsJson = "[]";
        ArtifactsProducedJson = "[]";
        Status = "Running";
    }

    public void UpdatePlan(string planJson)
    {
        PlanJson = planJson;
    }

    public void UpdateSteps(string stepsJson)
    {
        StepsJson = stepsJson;
    }

    public void AddLinkedAiRun(Guid aiRunId, string linkedAiRunIdsJson)
    {
        LinkedAiRunIdsJson = linkedAiRunIdsJson;
    }

    public void UpdateArtifacts(string artifactsProducedJson)
    {
        ArtifactsProducedJson = artifactsProducedJson;
    }

    public void Complete()
    {
        Status = "Completed";
    }

    public void Fail()
    {
        Status = "Failed";
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }
}
