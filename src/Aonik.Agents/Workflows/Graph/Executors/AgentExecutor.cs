using System.Text.Json;
using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Aonik.Agents.Workflows.Graph.Executors;

/// <summary>
/// MAF executor that runs a domain agent for an editor "agent" node.
///
/// <para><b>Param shape:</b> <c>{ "agent": "Billing", "task": "..." }</c>.
/// The agent name is resolved through <see cref="IDomainAgentResolver"/>;
/// the task is composed with the upstream message and sent as the user
/// message. The agent's text response is forwarded to the next executor.</para>
/// </summary>
internal sealed class AgentExecutor : Executor<string, string>
{
    private readonly Guid _nodeId;
    private readonly string _agentName;
    private readonly string _task;
    private readonly IDomainAgentResolver _resolver;
    private readonly IWorkflowRunRecorder _recorder;

    public AgentExecutor(
        Guid nodeId,
        string agentName,
        string task,
        IDomainAgentResolver resolver,
        IWorkflowRunRecorder recorder)
        : base($"agent-{nodeId:N}")
    {
        _nodeId = nodeId;
        _agentName = agentName;
        _task = task;
        _resolver = resolver;
        _recorder = recorder;
    }

    public override async ValueTask<string> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _recorder.RecordVisit(_nodeId);

        AIAgent agent;
        string? configuredModel = null;
        try
        {
            var resolution = await _resolver.ResolveAsync(_agentName, cancellationToken);
            agent = resolution.Agent;
            configuredModel = resolution.ConfiguredModelName;
        }
        catch (Exception)
        {
            // Resolver throws when the named domain agent is not
            // registered. Surface as advisory output rather than
            // failing the whole workflow run.
            return $"[agent:{_agentName}] not found — workflow advisory.";
        }

        var prompt = string.IsNullOrWhiteSpace(_task)
            ? message ?? string.Empty
            : string.IsNullOrWhiteSpace(message)
                ? _task
                : $"{_task}\n\nPrior step output:\n{message}";

        // Apply the per-agent model override if configured. Without this
        // the workflow agent inherits the chat client's global default
        // (same gap that the AGUI endpoint had).
        AgentRunOptions? runOptions = null;
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new Microsoft.Extensions.AI.ChatOptions
                {
                    ModelId = configuredModel,
                },
            };
        }

        var response = await agent.RunAsync(prompt, options: runOptions, cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public static (string Agent, string Task) ParseParams(string paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson)) return (string.Empty, string.Empty);
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            var root = doc.RootElement;
            var agent = root.TryGetProperty("agent", out var a) ? a.GetString() ?? string.Empty : string.Empty;
            var task = root.TryGetProperty("task", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            return (agent, task);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
