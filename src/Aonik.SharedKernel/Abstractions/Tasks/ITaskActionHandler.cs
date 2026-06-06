namespace Aonik.SharedKernel.Abstractions.Tasks;

/// <summary>
/// The pluggable "what to do when a task is due" seam (Spec 034). A due
/// <c>WorkItem</c> does not carry its own execution logic; it carries an
/// <c>ActionType</c> string and the dispatcher resolves a keyed
/// handler for it — the identical keyed-DI pattern AONIK already uses for
/// <c>IProposalHandler</c> (Spec 030). The contract lives in SharedKernel so any
/// module can implement one without depending on the Platform task code.
/// </summary>
/// <remarks>
/// Handlers register as keyed scoped services in their owning module, keyed by
/// <see cref="ActionType"/>, e.g.
/// <c>services.AddKeyedScoped&lt;ITaskActionHandler, NotifyUserTaskActionHandler&gt;("notify_user")</c>.
/// The propose-don't-execute rule applies with full force: a handler for a
/// high-risk action MUST NOT call a money/ledger/partner service directly — its
/// only permitted effect is to create a <c>Proposal</c> (Spec 030) and return
/// <see cref="TaskActionOutcome.Proposed"/>. <see cref="TaskActionContext.ActionPayloadJson"/>
/// is opaque, handler-validated, and must be treated as untrusted input.
/// </remarks>
public interface ITaskActionHandler
{
    /// <summary>The <c>ActionType</c> this handler is registered for; doubles as its DI key (e.g. "notify_user").</summary>
    string ActionType { get; }

    /// <summary>Executes the action for a single due occurrence and reports a structured result.</summary>
    Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Everything a <see cref="ITaskActionHandler"/> needs to act on one due occurrence.
/// The ambient tenant context is already set to <see cref="TenantId"/> before the
/// handler is invoked, so all of the handler's tenant-scoped writes are correct.
/// </summary>
public sealed record TaskActionContext(
    Guid TenantId,
    Guid WorkItemId,
    Guid RunId,
    string Kind,
    string? SubjectType,
    Guid? SubjectId,
    string AssigneeType,
    Guid? AssigneeId,
    string? AssigneeKey,
    DateTime ScheduledForUtc,
    string ActionPayloadJson);

/// <summary>
/// The outcome of a single due-occurrence dispatch. Stamped onto the
/// <c>WorkItemRun</c> audit row; <see cref="AiRunId"/>/<see cref="ProposalId"/>
/// link the chain "task → run → AI run / proposal → approval → execution".
/// </summary>
public sealed record TaskActionResult(
    TaskActionOutcome Outcome,
    string? ResultJson = null,
    string? Error = null,
    Guid? AiRunId = null,
    Guid? ProposalId = null);

/// <summary>The terminal disposition a handler reports for one due occurrence.</summary>
public enum TaskActionOutcome
{
    /// <summary>The action ran and succeeded in-band (reversible, no money moved).</summary>
    Succeeded = 1,

    /// <summary>The action could not be completed; the dispatcher decides retry vs. skip.</summary>
    Failed = 2,

    /// <summary>The handler deliberately did nothing (e.g. the subject no longer warrants it).</summary>
    Skipped = 3,

    /// <summary>A high-risk action marshalled into a <c>Proposal</c>; money moves only after approval.</summary>
    Proposed = 4,
}
