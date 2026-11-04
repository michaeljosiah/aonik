namespace Aonik.SharedKernel.Events.Integration;

public sealed record PublicationOperationRequestedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid OperationId) : IIntegrationEvent, ITenantScopedEvent;

public sealed record PublicationCatalogueCommittedEvent(Guid EventId, DateTime OccurredAt, Guid TenantId, string ChannelKey, long Serial) : IIntegrationEvent, ITenantScopedEvent;
