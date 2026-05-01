namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Captures the per-run trace of visited nodes so the registry's
/// run-history panel can replay the path the workflow took. The recorder
/// is scoped to a single workflow run; <see cref="GraphWorkflowFactory"/>
/// constructs a fresh instance per build.
/// </summary>
public interface IWorkflowRunRecorder
{
    /// <summary>Mark a node as visited. Called by each executor on entry.</summary>
    void RecordVisit(Guid nodeId);

    /// <summary>The ordered sequence of visited node ids.</summary>
    IReadOnlyList<Guid> Sequence { get; }
}

/// <summary>
/// Default in-memory recorder. Each workflow run gets a fresh instance.
/// Persistence (writing a <c>WorkflowRun</c> row) happens in the factory
/// after the workflow finishes — keeping the recorder I/O-free means
/// executors stay synchronous on the hot path.
/// </summary>
internal sealed class WorkflowRunRecorder : IWorkflowRunRecorder
{
    private readonly List<Guid> _sequence = new();
    private readonly Lock _gate = new();

    public void RecordVisit(Guid nodeId)
    {
        lock (_gate)
        {
            _sequence.Add(nodeId);
        }
    }

    public IReadOnlyList<Guid> Sequence
    {
        get
        {
            lock (_gate)
            {
                return _sequence.ToArray();
            }
        }
    }
}
