using System.Text.Json;
using Aonik.SharedKernel.Events;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>
/// MAF executor for the editor "notify" node. Publishes a
/// <see cref="WorkflowNotifyRequestedEvent"/> onto the in-process event
/// bus so a Platform-side handler can dispatch through email / SMS / etc.
/// We keep dispatch out of process here because Agents must not depend
/// on Platform directly (see CLAUDE.md module-boundary rules).
///
/// <para><b>Param shape:</b> <c>{ "channel": "email", "template": "receipt_v2" }</c>.
/// The recipient is supplied by the trigger payload, which the trigger
/// ingest subsystem (out of scope for this iteration) needs to thread
/// through. Until then the recipient field on the event is empty and the
/// Platform handler should treat the event as advisory.</para>
/// </summary>
internal sealed class NotifyExecutor : Executor<string, string>
{
    private readonly Guid _nodeId;
    private readonly string _channel;
    private readonly string _template;
    private readonly IEventBus _eventBus;
    private readonly IWorkflowRunRecorder _recorder;
    private readonly ILogger<NotifyExecutor> _logger;

    public NotifyExecutor(
        Guid nodeId,
        string channel,
        string template,
        IEventBus eventBus,
        IWorkflowRunRecorder recorder,
        ILogger<NotifyExecutor> logger)
        : base($"notify-{nodeId:N}")
    {
        _nodeId = nodeId;
        _channel = channel;
        _template = template;
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

        var notifyEvent = new WorkflowNotifyRequestedEvent(
            Channel: _channel ?? string.Empty,
            Template: _template ?? string.Empty,
            Recipient: string.Empty,
            Body: message ?? string.Empty);
        await _eventBus.PublishAsync(notifyEvent, cancellationToken);
        _logger.LogDebug(
            "[Notify] Published notification request channel={Channel} template={Template} from node {NodeId}.",
            _channel, _template, _nodeId);

        return message ?? string.Empty;
    }

    public static (string Channel, string Template) ParseParams(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson)) return (string.Empty, string.Empty);
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            var root = doc.RootElement;
            var channel = root.TryGetProperty("channel", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            var template = root.TryGetProperty("template", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            return (channel, template);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }
}

/// <summary>
/// Integration event raised by a workflow's "notify" node. Platform
/// subscribers (email, SMS) pick it up and dispatch.
/// </summary>
public sealed record WorkflowNotifyRequestedEvent(
    string Channel,
    string Template,
    string Recipient,
    string Body) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
