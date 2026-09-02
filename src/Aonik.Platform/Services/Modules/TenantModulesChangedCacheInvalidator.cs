using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Platform.Services.Modules;

/// <summary>
/// Cache invalidation for module enablement (Spec 097 §7). The write side publishes
/// <see cref="TenantModulesChangedEvent"/>; this handler (discovered by
/// <c>AddEventHandlersFromAssembly</c> in <c>PlatformModule</c>) drops the tenant's cached set so the
/// next read resolves from the store.
/// </summary>
/// <remarks>
/// <para>
/// The handler runs only where the event is actually dispatched, which today means two places:
/// in-process in the publishing host when the write side publishes through <c>IEventBus</c>
/// (<c>InProcessEventBus</c> resolves this handler from the same scope), and in the Worker, which is
/// the only host that drains the transactional outbox (<c>AddOutboxProcessing</c> is registered in
/// <c>Aonik.Worker</c> alone). The API does not drain the outbox and FusionCache is registered
/// memory-only (no distributed L2, no backplane), so the outbox path never reaches an API process.
/// </para>
/// <para>
/// Consequences for the write-side slice: <c>TenantModuleService</c>'s update path must call
/// <see cref="TenantModuleService.InvalidateAsync"/> directly and publish the event in-process via
/// <c>IEventBus</c> in addition to enqueueing it on the outbox; it must not rely on the outbox to
/// refresh its own host. Any other API replica keeps serving the previous set for at most one cache
/// entry lifetime — 60 seconds (<see cref="TenantModuleService"/>), the bound a multi-replica
/// deployment should plan around; fail-safe can stretch it to one hour only while the store itself
/// is unreadable. Closing that window entirely needs a FusionCache backplane or distributed L2,
/// which is out of scope for P1.
/// </para>
/// </remarks>
internal sealed class TenantModulesChangedCacheInvalidator : IEventHandler<TenantModulesChangedEvent>
{
    private readonly TenantModuleService _tenantModuleService;

    public TenantModulesChangedCacheInvalidator(TenantModuleService tenantModuleService)
    {
        _tenantModuleService = tenantModuleService;
    }

    public Task HandleAsync(TenantModulesChangedEvent @event, CancellationToken cancellationToken = default)
        => _tenantModuleService.InvalidateAsync(@event.TenantId, cancellationToken);
}
