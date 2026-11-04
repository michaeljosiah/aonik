namespace Aonik.SharedKernel.Events.Integration;

public sealed record PublicationValidationRequestedEvent(
    Guid EventId, DateTime OccurredAt, Guid TenantId, Guid SubmissionId, Guid RevisionId, Guid ReportId)
    : ITenantScopedEvent;
