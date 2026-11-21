namespace Aonik.SharedKernel.Events.Integration;

public sealed record ConnectorRequestWakeRequestedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid RequestId,
    string Kind, DateTime NotBeforeUtc, Guid? RecipientUserId = null) : IIntegrationEvent;
