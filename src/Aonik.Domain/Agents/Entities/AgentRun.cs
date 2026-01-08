using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Agents.Entities;

public class AgentRun : AuditableEntity, ITenantScoped
{
    public Guid AgentRunId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string PlanJson { get; set; } = string.Empty;
    public string StepsJson { get; set; } = string.Empty;
    public string LinkedAiRunIdsJson { get; set; } = string.Empty;
    public string ArtifactsProducedJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
