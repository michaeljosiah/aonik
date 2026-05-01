using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Workflows.Graph.Executors;
using Aonik.SharedKernel.Events;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowNodeKinds = Aonik.Agents.Entities.Workflows.WorkflowNodeKinds;

namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Translates a saved editor graph (<c>Workflow</c> + <c>WorkflowNode</c>
/// + <c>WorkflowEdge</c> rows) into a Microsoft Agent Framework
/// <see cref="Workflow"/> instance.
///
/// <para>The legacy keyed factories ([Invoice|Onboarding|Reconciliation]
/// WorkflowFactory) wrap their workflow with <c>.AsAIAgent(...)</c>
/// because they're built from <see cref="AgentWorkflowBuilder.BuildSequential"/>
/// — that path produces a workflow whose start executor speaks the
/// MAF chat protocol (<c>List&lt;ChatMessage&gt;</c> + <c>TurnToken</c>).
/// Our custom executors (<see cref="AgentExecutor"/>, etc.) speak plain
/// <c>string</c>, so wrapping them with <c>.AsAIAgent()</c> fails at run
/// time with "Workflow does not support ChatProtocol". Instead, the
/// caller invokes the workflow through
/// <see cref="GraphWorkflowRunner"/> which uses
/// <see cref="InProcessExecution.RunAsync{TInput}"/> with a string
/// payload directly.</para>
///
/// <para><b>Per-kind mapping:</b>
/// <list type="bullet">
///   <item><c>trigger</c> — has no executor; the first downstream node
///   becomes the workflow's start.</item>
///   <item><c>agent</c> — <see cref="AgentExecutor"/>.</item>
///   <item><c>notify</c>, <c>emit</c>, <c>end</c> — wired through their
///   respective executor classes.</item>
///   <item><c>tool</c>, <c>decision</c>, <c>loop</c>, <c>human</c>, <c>wait</c>
///   — translated to <see cref="UnsupportedKindExecutor"/> which throws
///   <see cref="NotSupportedException"/> when the workflow runs. Deferred
///   to follow-up PRs (NCalc decisions, Quartz-backed waits, HITL via
///   MAF's <c>RequestInfoExecutor</c> + checkpointing).</item>
/// </list>
/// </para>
/// </summary>
internal static class GraphWorkflowBuilder
{
    /// <summary>
    /// Loads the workflow graph identified by <paramref name="slug"/> and
    /// builds a runnable MAF <see cref="Workflow"/>.
    /// </summary>
    /// <param name="slug">The workflow slug as stored on the
    /// <c>Workflow</c> row (e.g. <c>"match_and_apply"</c>).</param>
    /// <param name="sp">Service provider used to resolve dependencies for
    /// each executor (<see cref="IDomainAgentResolver"/>,
    /// <see cref="IEventBus"/>, etc.).</param>
    /// <param name="recorder">Receives a <see cref="WorkflowRunRecorder"/>
    /// instance the caller can inspect after the run finishes for the
    /// visited-node sequence.</param>
    public static Workflow Build(string slug, IServiceProvider sp, out IWorkflowRunRecorder recorder)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug is required.", nameof(slug));
        }

        var workflowService = sp.GetRequiredService<IWorkflowService>();
        var graph = workflowService.GetBySlugAsync(slug).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException(
                $"Workflow '{slug}' not found — cannot build runtime workflow.");

        if (graph.Nodes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Workflow '{slug}' has no nodes — nothing to run.");
        }

        var run = new WorkflowRunRecorder();
        recorder = run;

        var executors = BuildExecutors(graph, run, sp);

        var trigger = graph.Nodes.FirstOrDefault(n =>
            string.Equals(n.Kind, WorkflowNodeKinds.Trigger, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Workflow '{slug}' has no trigger node — cannot determine start.");

        var startEdge = graph.Edges.FirstOrDefault(e => e.FromNodeId == trigger.Id)
            ?? throw new InvalidOperationException(
                $"Workflow '{slug}' trigger has no downstream edge — workflow has no entry point.");

        if (!executors.TryGetValue(startEdge.ToNodeId, out var startExecutor))
        {
            throw new InvalidOperationException(
                $"Workflow '{slug}' references unknown start node {startEdge.ToNodeId}.");
        }

        var builder = new WorkflowBuilder(startExecutor)
            .WithName(graph.Name)
            .WithDescription(graph.Description);

        foreach (var edge in graph.Edges)
        {
            // Skip edges whose source is the trigger — the trigger is
            // virtual and the start executor is already its downstream.
            if (edge.FromNodeId == trigger.Id) continue;

            if (!executors.TryGetValue(edge.FromNodeId, out var from)
                || !executors.TryGetValue(edge.ToNodeId, out var to))
            {
                continue;
            }

            // Conditional routing for decision / loop nodes is deferred
            // along with those executors. Until then every edge is
            // unconditional, which is correct for trigger / agent /
            // notify / emit chains. The UnsupportedKindExecutor throws
            // before any branching choice would be needed.
            builder.AddEdge(from, to);
        }

        return builder.Build();
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
                return new EndExecutor(node.Id, recorder, loggerFactory.CreateLogger<EndExecutor>());

            // Deferred kinds — see GraphWorkflowBuilder class doc.
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
