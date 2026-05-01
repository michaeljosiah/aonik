using Microsoft.Agents.AI.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>
/// MAF executor that terminates the workflow by yielding the inbound
/// message as the workflow's output. Maps to the editor "end" node kind.
/// </summary>
internal sealed class EndExecutor : Executor<string>
{
    private readonly Guid _nodeId;
    private readonly IWorkflowRunRecorder _recorder;

    public EndExecutor(Guid nodeId, IWorkflowRunRecorder recorder)
        : base($"end-{nodeId:N}")
    {
        _nodeId = nodeId;
        _recorder = recorder;
    }

    /// <summary>
    /// MAF validates yielded output against the protocol's declared
    /// yield types — without this declaration the run fails with
    /// "Cannot output object of type String. Expecting one of []".
    /// </summary>
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocol)
        => base.ConfigureProtocol(protocol).YieldsOutput<string>();

    public override async ValueTask HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _recorder.RecordVisit(_nodeId);
        await context.YieldOutputAsync(message ?? string.Empty, cancellationToken);
    }
}
