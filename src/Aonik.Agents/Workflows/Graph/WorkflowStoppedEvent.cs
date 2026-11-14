using Microsoft.Agents.AI.Workflows;

namespace Aonik.Agents.Workflows.Graph;

internal sealed class WorkflowStoppedEvent(WorkflowExecutionFrame frame) : WorkflowEvent(frame) { }
