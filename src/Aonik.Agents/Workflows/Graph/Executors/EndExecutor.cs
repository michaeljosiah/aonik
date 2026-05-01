using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>
/// MAF executor that terminates the workflow by yielding the inbound
/// message as the workflow's output. Maps to the editor "end" node kind.
/// </summary>
internal sealed class EndExecutor : Executor<string>
{
    private readonly Guid _nodeId;
    private readonly IWorkflowRunRecorder _recorder;
    private readonly ILogger<EndExecutor>? _logger;

    public EndExecutor(Guid nodeId, IWorkflowRunRecorder recorder, ILogger<EndExecutor>? logger = null)
        : base($"end-{nodeId:N}")
    {
        _nodeId = nodeId;
        _recorder = recorder;
        _logger = logger;
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
        var payload = message ?? string.Empty;
        _logger?.LogInformation(
            "[End] Yielding output (len={Len}): {Preview}",
            payload.Length,
            payload.Length > 80 ? payload[..80] + "…" : payload);
        // Side-channel record so the runner can return the output even
        // when MAF rc4's event stream withholds the WorkflowOutputEvent.
        _recorder.RecordOutput(payload);
        await context.YieldOutputAsync(payload, cancellationToken);
    }
}
