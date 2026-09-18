namespace Aonik.SharedKernel.Events;

/// <summary>
/// An integration event that belongs to one tenant (Spec 097 §12, §14). Lets the dispatcher — and
/// any handler that reacts per tenant — know which tenant an event is about without inspecting the
/// payload, so module-gated handlers can be skipped for tenants that have the module off.
/// </summary>
public interface ITenantScopedEvent : IIntegrationEvent
{
    Guid TenantId { get; }
}
