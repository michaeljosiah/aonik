using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>
/// Placeholder executor for editor node kinds the runtime doesn't
/// implement yet (tool, decision, loop, human, wait). Graphs containing
/// these kinds save fine — the unsupported semantics surface only at
/// run time, with a loud failure rather than a silent no-op.
///
/// <para>When you implement one of these kinds, replace its registration
/// in <see cref="GraphWorkflowFactory"/> with the new typed executor.</para>
/// </summary>
internal sealed class UnsupportedKindExecutor : Executor<string, string>
{
    private readonly Guid _nodeId;
    private readonly string _kind;
    private readonly IWorkflowRunRecorder _recorder;
    private readonly ILogger<UnsupportedKindExecutor> _logger;

    public UnsupportedKindExecutor(
        Guid nodeId,
        string kind,
        IWorkflowRunRecorder recorder,
        ILogger<UnsupportedKindExecutor> logger)
        : base($"unsupported-{kind}-{nodeId:N}")
    {
        _nodeId = nodeId;
        _kind = kind;
        _recorder = recorder;
        _logger = logger;
    }

    public override ValueTask<string> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _recorder.RecordVisit(_nodeId);
        _logger.LogError(
            "Workflow node {NodeId} of kind '{Kind}' is not yet implemented in the graph runtime. " +
            "See deferred-work notes in GraphWorkflowFactory.",
            _nodeId, _kind);
        throw new NotSupportedException(
            $"Workflow node kind '{_kind}' is not yet implemented in the graph runtime. " +
            "Supported kinds in this iteration: trigger, agent, notify, emit, end. " +
            "Tool / decision / loop / human / wait executors are deferred.");
    }
}
