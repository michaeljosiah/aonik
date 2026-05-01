using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Runs a saved editor workflow by slug. <see cref="Endpoints.RunWorkflowEndpoint"/>
/// falls through to this when no keyed legacy factory matches — every
/// editor-saved workflow becomes runnable through this path.
/// </summary>
public interface IGraphWorkflowRunner
{
    /// <summary>
    /// Resolves the workflow graph by <paramref name="slug"/>, builds the
    /// MAF <see cref="Workflow"/>, runs it with <paramref name="input"/>
    /// as a string seed, and returns the workflow's terminal output (the
    /// content the <c>End</c> executor yields).
    /// </summary>
    Task<GraphWorkflowResult> RunAsync(string slug, string input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a graph workflow run. Carries the terminal output (from the
/// End executor's <see cref="IWorkflowContext.YieldOutputAsync"/> call)
/// plus the visited-node sequence the
/// <see cref="IWorkflowRunRecorder"/> captured.
/// </summary>
public sealed record GraphWorkflowResult(string Output, IReadOnlyList<Guid> Sequence);

internal sealed class GraphWorkflowRunner : IGraphWorkflowRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GraphWorkflowRunner> _logger;

    public GraphWorkflowRunner(IServiceProvider serviceProvider, ILogger<GraphWorkflowRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<GraphWorkflowResult> RunAsync(
        string slug,
        string input,
        CancellationToken cancellationToken = default)
    {
        // Build the workflow inside a fresh DI scope so per-run scoped
        // services (DbContext, IDomainAgentResolver, IEventBus impls)
        // are isolated to this run and disposed when it ends.
        await using var scope = _serviceProvider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var workflow = GraphWorkflowBuilder.Build(slug, sp, out var recorder);

        var run = await InProcessExecution.RunAsync<string>(
            workflow,
            input ?? string.Empty,
            cancellationToken: cancellationToken);

        // Drain the events emitted by the run. RunAsync halts at the
        // first stop, so any WorkflowOutputEvent is already captured.
        // MAF surfaces executor exceptions as ExecutorFailedEvents
        // rather than propagating them out of RunAsync — re-throw the
        // first one we see so the endpoint reports a failed run instead
        // of silently returning success with empty output.
        var output = string.Empty;
        Exception? failure = null;
        var eventTypes = new List<string>();
        foreach (var ev in run.NewEvents)
        {
            eventTypes.Add(ev.GetType().Name);
            switch (ev)
            {
                case WorkflowOutputEvent oe when oe.Data is not null:
                    output = oe.Data.ToString() ?? string.Empty;
                    break;
                case ExecutorFailedEvent fe when fe.Data is Exception err && failure is null:
                    failure = err;
                    break;
            }
        }
        _logger.LogInformation(
            "Graph workflow '{Slug}' emitted {Count} events: {Types}, output len={OutLen}",
            slug, eventTypes.Count, string.Join(", ", eventTypes), output.Length);

        if (failure is not null)
        {
            _logger.LogWarning(
                "Graph workflow '{Slug}' failed after {Count} step(s): {Error}",
                slug, recorder.Sequence.Count, failure.Message);
            throw failure;
        }

        _logger.LogInformation(
            "Graph workflow '{Slug}' completed with sequence length {Count}.",
            slug, recorder.Sequence.Count);

        return new GraphWorkflowResult(output, recorder.Sequence);
    }
}
