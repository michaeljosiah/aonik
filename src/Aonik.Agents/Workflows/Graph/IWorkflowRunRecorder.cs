namespace Aonik.Agents.Workflows.Graph;

/// <summary>
/// Captures the per-run trace of visited nodes plus the workflow's
/// terminal output. MAF rc4 doesn't surface
/// <see cref="Microsoft.Agents.AI.Workflows.WorkflowOutputEvent"/>
/// through <c>Run.NewEvents</c> or <c>StreamingRun.WatchStreamAsync</c>
/// in a way our runner can read, so the End executor stamps its yielded
/// payload here as a side channel the runner reads after the workflow
/// halts.
/// </summary>
public interface IWorkflowRunRecorder
{
    /// <summary>Mark a node as visited. Called by each executor on entry.</summary>
    void RecordVisit(Guid nodeId);

    /// <summary>Capture the terminal output yielded by the End executor.</summary>
    void RecordOutput(string output);

    /// <summary>The ordered sequence of visited node ids.</summary>
    IReadOnlyList<Guid> Sequence { get; }

    /// <summary>The terminal output, or empty string if End wasn't reached.</summary>
    string Output { get; }
}

internal sealed class WorkflowRunRecorder : IWorkflowRunRecorder
{
    private readonly List<Guid> _sequence = new();
    private readonly Lock _gate = new();
    private string _output = string.Empty;

    public void RecordVisit(Guid nodeId)
    {
        lock (_gate)
        {
            _sequence.Add(nodeId);
        }
    }

    public void RecordOutput(string output)
    {
        lock (_gate)
        {
            _output = output ?? string.Empty;
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

    public string Output
    {
        get
        {
            lock (_gate)
            {
                return _output;
            }
        }
    }
}
