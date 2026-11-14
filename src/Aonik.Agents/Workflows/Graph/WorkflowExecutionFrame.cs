namespace Aonik.Agents.Workflows.Graph;

internal sealed record WorkflowExecutionFrame(Guid RunId, string? Route = null, string? Outcome = null, bool Stopped = false);
internal sealed record WorkflowStepWait(Guid RunId, Guid StepRunId);
