using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Workflows.Graph.Executors;
using Aonik.SharedKernel.Events;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Generic <see cref="IWorkflowFactory"/> that hydrates a saved editor
/// graph (<c>Workflow</c> + <c>WorkflowNode</c> + <c>WorkflowEdge</c> rows)
/// into a Microsoft Agent Framework <see cref="Workflow"/> at run time.
///
/// <para><b>Mapping:</b>
/// <list type="bullet">
///   <item><c>trigger</c> — has no executor; the first downstream node
///   becomes the workflow's start.</item>
///   <item><c>agent</c> — <see cref="AgentExecutor"/> resolving the named
///   domain agent via <see cref="IDomainAgentResolver"/>.</item>
///   <item><c>notify</c>, <c>emit</c>, <c>end</c> — wired through their
///   respective executor classes.</item>
///   <item><c>tool</c>, <c>decision</c>, <c>loop</c>, <c>human</c>, <c>wait</c>
///   — translated to <see cref="UnsupportedKindExecutor"/> which throws
///   <see cref="NotSupportedException"/> at run time. These are deferred
///   to follow-up PRs (NCalc decisions, Quartz-backed waits, HITL via
///   MAF's <c>RequestInfoExecutor</c> + checkpointing).</item>
/// </list>
/// </para>
///
/// <para>One factory instance per slug. <see cref="IWorkflowFactory.Build"/>
/// is called per workflow run, which keeps the per-run
/// <see cref="IWorkflowRunRecorder"/> isolated.</para>
/// </summary>
internal sealed class GraphWorkflowFactory : IWorkflowFactory
{
    private readonly string _slug;

    public GraphWorkflowFactory(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug is required.", nameof(slug));
        }
        _slug = slug;
    }

    public string WorkflowName => _slug;

    public AIAgent Build(IServiceProvider serviceProvider)
    {
        var workflowService = serviceProvider.GetRequiredService<IWorkflowService>();
        var graph = workflowService.GetBySlugAsync(_slug).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException(
                $"Workflow '{_slug}' not found — cannot build runtime workflow.");

        if (graph.Nodes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Workflow '{_slug}' has no nodes — nothing to run.");
        }

        // Per-run recorder — we want a fresh sequence list per Build call.
        var recorder = new WorkflowRunRecorder();

        // Build executors for every non-trigger node. The trigger is
        // metadata; the workflow's start is the trigger's first downstream.
        var executors = BuildExecutors(graph, recorder, serviceProvider);

        var trigger = graph.Nodes.FirstOrDefault(n =>
            string.Equals(n.Kind, WorkflowNodeKinds.Trigger, StringComparison.OrdinalIgnoreCase));
        if (trigger is null)
        {
            throw new InvalidOperationException(
                $"Workflow '{_slug}' has no trigger node — cannot determine start.");
        }
        var startEdge = graph.Edges.FirstOrDefault(e => e.FromNodeId == trigger.Id);
        if (startEdge is null)
        {
            throw new InvalidOperationException(
                $"Workflow '{_slug}' trigger has no downstream edge — workflow has no entry point.");
        }
        if (!executors.TryGetValue(startEdge.ToNodeId, out var startExecutor))
        {
            throw new InvalidOperationException(
                $"Workflow '{_slug}' references unknown start node {startEdge.ToNodeId}.");
        }

        var builder = new WorkflowBuilder(startExecutor)
            .WithName(graph.Name)
            .WithDescription(graph.Description);

        // Wire edges. Skip edges whose source is the trigger — the trigger
        // is virtual and the start executor is already the trigger's
        // downstream.
        foreach (var edge in graph.Edges)
        {
            if (edge.FromNodeId == trigger.Id) continue;

            if (!executors.TryGetValue(edge.FromNodeId, out var from)
                || !executors.TryGetValue(edge.ToNodeId, out var to))
            {
                continue; // Edge references a node not in the graph — skip.
            }

            // Conditional routing for decision / loop nodes is deferred
            // along with those executors. Until then every edge is
            // unconditional, which is correct for trigger / agent /
            // notify / emit chains. The UnsupportedKindExecutor throws
            // before any branching choice would be needed.
            builder.AddEdge(from, to);
        }

        var workflow = builder.Build();
        return workflow.AsAIAgent(
            id: _slug,
            name: graph.Name,
            description: graph.Description);
    }

    private static Dictionary<Guid, Executor> BuildExecutors(
        WorkflowGraphResponse graph,
        IWorkflowRunRecorder recorder,
        IServiceProvider sp)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var executors = new Dictionary<Guid, Executor>(graph.Nodes.Count);

        foreach (var node in graph.Nodes)
        {
            // Triggers don't get an executor — they're virtual.
            if (string.Equals(node.Kind, WorkflowNodeKinds.Trigger, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            executors[node.Id] = BuildExecutor(node, recorder, sp, loggerFactory);
        }
        return executors;
    }

    private static Executor BuildExecutor(
        WorkflowGraphNode node,
        IWorkflowRunRecorder recorder,
        IServiceProvider sp,
        ILoggerFactory loggerFactory)
    {
        var kind = (node.Kind ?? string.Empty).ToLowerInvariant();
        switch (kind)
        {
            case "agent":
            {
                var (agentName, task) = AgentExecutor.ParseParams(node.ParamsJson);
                var resolver = sp.GetRequiredService<IDomainAgentResolver>();
                return new AgentExecutor(node.Id, agentName, task, resolver, recorder);
            }
            case "notify":
            {
                var (channel, template) = NotifyExecutor.ParseParams(node.ParamsJson);
                return new NotifyExecutor(
                    node.Id,
                    channel,
                    template,
                    sp.GetRequiredService<IEventBus>(),
                    recorder,
                    loggerFactory.CreateLogger<NotifyExecutor>());
            }
            case "emit":
            {
                var eventName = EmitExecutor.ParseEventName(node.ParamsJson);
                return new EmitExecutor(
                    node.Id,
                    eventName,
                    sp.GetRequiredService<IEventBus>(),
                    recorder,
                    loggerFactory.CreateLogger<EmitExecutor>());
            }
            case "end":
                return new EndExecutor(node.Id, recorder);

            // Deferred kinds — see GraphWorkflowFactory class doc.
            case "tool":
            case "decision":
            case "loop":
            case "human":
            case "wait":
                return new UnsupportedKindExecutor(
                    node.Id,
                    kind,
                    recorder,
                    loggerFactory.CreateLogger<UnsupportedKindExecutor>());

            default:
                return new UnsupportedKindExecutor(
                    node.Id,
                    kind,
                    recorder,
                    loggerFactory.CreateLogger<UnsupportedKindExecutor>());
        }
    }
}
