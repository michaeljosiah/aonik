using System.Text.Json;
using Aonik.SharedKernel.Events;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>
/// MAF executor for the editor "emit" node. Publishes a generic workflow
/// event onto the in-process event bus so downstream subscribers
/// (alerting, analytics, other workflows) can react.
///
/// <para><b>Param shape:</b> <c>{ "event": "invoice.matched" }</c>.</para>
/// </summary>
internal sealed class EmitExecutor : Executor<string, string>
{
    private readonly Guid _nodeId;
    private readonly string _eventName;
    private readonly IEventBus _eventBus;
    private readonly IWorkflowRunRecorder _recorder;
    private readonly ILogger<EmitExecutor> _logger;

    public EmitExecutor(
        Guid nodeId,
        string eventName,
        IEventBus eventBus,
        IWorkflowRunRecorder recorder,
        ILogger<EmitExecutor> logger)
        : base($"emit-{nodeId:N}")
    {
        _nodeId = nodeId;
        _eventName = eventName;
        _eventBus = eventBus;
        _recorder = recorder;
        _logger = logger;
    }

    public override async ValueTask<string> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _recorder.RecordVisit(_nodeId);

        var integrationEvent = new WorkflowEmittedEvent(_eventName, message ?? string.Empty);
        await _eventBus.PublishAsync(integrationEvent, cancellationToken);
        _logger.LogDebug(
            "[Emit] Published workflow event '{EventName}' from node {NodeId}.",
            _eventName, _nodeId);
        return message ?? string.Empty;
    }

    public static string ParseEventName(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            return doc.RootElement.TryGetProperty("event", out var e)
                ? e.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// Generic integration event emitted by a workflow's "emit" node.
/// Carries the user-supplied event name and the workflow message body
/// at the moment of emission.
/// </summary>
public sealed record WorkflowEmittedEvent(string EventName, string Payload) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
