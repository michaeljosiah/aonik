namespace Aonik.SharedKernel.Events.Integration;

public sealed record InvoiceLifecycleChangedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid InvoiceId,
    string Transition, Guid? CausationRunId = null) : ITenantScopedEvent;
public sealed record ConnectorMessageAddedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid ConnectionId,
    string MessageId, string ThreadId, DateTime ReceivedAtUtc, IReadOnlyList<string> LabelIds,
    Guid? CausationRunId = null, bool SelfGenerated = false) : ITenantScopedEvent;
public sealed record ConnectorMailboxHintEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid SubscriptionId,
    string HistoryId) : ITenantScopedEvent;
public sealed record WorkflowTriggerChangedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid BindingId,
    int Revision, string Change, Guid? PreviousTaskId = null, Guid? TaskId = null, string? ScheduleProof = null) : ITenantScopedEvent
{
    public override string ToString() => $"WorkflowTriggerChangedEvent({EventId:D}, {Change}, proof redacted)";
}
public sealed record WorkflowTriggerAdmissionRequestedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid BindingId,
    int Revision, Guid SourceEventId, DateTime SourceOccurredAtUtc, string SubjectType, Guid SubjectId,
    Guid? CausationRunId, string TaskProof, bool SelfGenerated = false) : ITenantScopedEvent
{
    public override string ToString() => $"WorkflowTriggerAdmissionRequestedEvent({EventId:D}, proof redacted)";
}
