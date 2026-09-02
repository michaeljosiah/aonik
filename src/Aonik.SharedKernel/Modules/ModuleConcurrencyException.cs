namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Thrown when a tenant's module set changed between the snapshot the dependency checks ran against
/// and the commit that would apply them (Spec 097 §9). Two toggles of related modules can each be
/// valid alone and invalid together — enabling Commerce while another request disables Finance — and
/// row versions do not catch it because the two requests write different rows. Nothing is written;
/// the caller re-reads the current set and re-submits.
/// </summary>
public sealed class ModuleConcurrencyException : Exception
{
    public ModuleConcurrencyException(Guid tenantId)
        : base($"The module set for tenant {tenantId} changed while this request was being applied. Re-read the current state and try again.")
    {
        TenantId = tenantId;
    }

    public Guid TenantId { get; }

    public string Code => ModuleErrorCodes.ConcurrentChange;
}
