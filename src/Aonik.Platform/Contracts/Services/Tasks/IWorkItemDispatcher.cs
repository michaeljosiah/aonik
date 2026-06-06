namespace Aonik.Platform.Contracts.Services.Tasks;

/// <summary>
/// Drains due <c>WorkItem</c>s (Spec 034). Invoked by the Worker's once-a-minute
/// Quartz heartbeat (<c>WorkItemDispatchJob</c>); kept separate from the job so the
/// dispatch logic is unit-testable without Quartz. Scans across tenants under a
/// system context, then processes each due item under its own tenant: claims a
/// time-boxed lease, writes a unique-per-occurrence run row, resolves the keyed
/// <c>ITaskActionHandler</c>, executes it, and re-arms recurrences.
/// </summary>
public interface IWorkItemDispatcher
{
    Task<WorkItemDispatchSummary> DispatchDueAsync(
        WorkItemDispatchOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Tunables for one dispatch sweep.</summary>
/// <param name="BatchSize">Max due items claimed per sweep.</param>
/// <param name="LeaseSeconds">How long a claimed item is hidden from other workers.</param>
/// <param name="MaxAttempts">Attempts per occurrence before it is failed (one-off) or skipped (recurring).</param>
public sealed record WorkItemDispatchOptions(
    int BatchSize = 100,
    int LeaseSeconds = 300,
    int MaxAttempts = 5);

/// <summary>Counts from one dispatch sweep, for the job's result line and logging.</summary>
public sealed record WorkItemDispatchSummary(
    int Considered,
    int Succeeded,
    int Proposed,
    int Skipped,
    int Failed)
{
    public static readonly WorkItemDispatchSummary Empty = new(0, 0, 0, 0, 0);
}
