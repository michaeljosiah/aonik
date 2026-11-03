namespace Aonik.SharedKernel.Events.Integration;

/// <summary>Reference-only notification work committed with a shared room message.</summary>
public sealed record ChatRoomMessagePostedEvent(
    Guid TenantId,
    Guid RoomId,
    Guid MessageId,
    IReadOnlyList<Guid> RecipientUserIds,
    DateTime OccurredAt) : IIntegrationEvent
{
    public Guid EventId => MessageId;
}
