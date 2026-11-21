using Microsoft.Agents.AI.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>MAF checkpoint state couples one join emission to its outgoing messages.</summary>
internal sealed class WorkflowParallelJoinOutput
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _emitted;

    public async Task SendAsync(Func<Task<WorkflowExecutionFrame?>> join, IWorkflowContext context, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_emitted || await context.ReadStateAsync<bool>("emitted", cancellationToken: cancellationToken)) return;
            WorkflowExecutionFrame? result = await join();
            if (result is null) return;
            _emitted = true;
            await context.QueueStateUpdateAsync("emitted", true, cancellationToken: cancellationToken);
            await context.SendMessageAsync(result, cancellationToken: cancellationToken);
        }
        finally { _gate.Release(); }
    }
}
