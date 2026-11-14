using Aonik.Agents.Entities.Workflows;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Set only by the workflow runner in its scoped continuation, never from tool arguments.</summary>
internal sealed class WorkflowInvocationContext
{
    public WorkflowRun? Run { get; set; }
    public WorkflowStepRun? Step { get; set; }
    public Guid LeaseId { get; set; }
    public Guid? OriginatingAiRunId { get; set; }
}
