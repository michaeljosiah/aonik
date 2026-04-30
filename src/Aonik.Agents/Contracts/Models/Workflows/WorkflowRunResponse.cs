namespace Aonik.Agents.Contracts.Models.Workflows;

/// <summary>
/// One execution of a workflow. Powers the "Recent runs" list in the detail
/// rail and the trace replay bar in the editor.
/// </summary>
public sealed record WorkflowRunResponse(
    Guid Id,
    DateTime StartedAt,
    DateTime? CompletedAt,
    /// <summary>Relative-time label used in the UI ("2m ago"). Computed server-side.</summary>
    string When,
    string Status,
    /// <summary>Human-readable duration string, e.g. "2.4s" or "7m 14s".</summary>
    string Duration,
    int DurationMs,
    string By,
    /// <summary>Ordered list of node ids visited in this run.</summary>
    IReadOnlyList<Guid> Sequence,
    int Total);
