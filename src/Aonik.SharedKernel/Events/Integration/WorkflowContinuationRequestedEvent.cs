namespace Aonik.SharedKernel.Events.Integration;

public sealed record WorkflowContinuationRequestedEvent(
    Guid EventId, DateTime OccurredAt, Guid TenantId, Guid RunId, int Continuation, DateTime NotBeforeUtc)
    : ITenantScopedEvent;
