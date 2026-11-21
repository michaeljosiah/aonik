namespace Aonik.Agents.Workflows.Graph;

internal sealed class WorkflowExecutionException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}
