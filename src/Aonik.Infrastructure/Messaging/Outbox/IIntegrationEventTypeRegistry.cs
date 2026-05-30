namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Resolves an integration event's persisted <see cref="System.Type.FullName"/>
/// back to its runtime <see cref="System.Type"/> so an outbox payload can be
/// deserialized and dispatched. Built once at startup by scanning the
/// event-carrying assemblies.
/// </summary>
public interface IIntegrationEventTypeRegistry
{
    /// <summary>Returns the event type for a stored type name, or null if unknown.</summary>
    Type? Resolve(string eventTypeName);
}
