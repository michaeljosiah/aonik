using Aonik.SharedKernel.Modules;

namespace Aonik.Platform.Contracts.Services.Modules;

/// <summary>
/// Thrown by <see cref="ITenantModuleService.UpdateAsync"/> when a module that transitions from off to
/// on cannot be provisioned: one of its <see cref="Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor"/>s
/// threw. The toggle is NOT persisted — the tenant's module state is exactly what it was before the
/// request — and the attempt is audited with the error. Contributors are idempotent, so the request
/// can simply be retried once the underlying fault is fixed. The HTTP layer maps it with
/// <see cref="Code"/> as the typed error code.
/// </summary>
public sealed class ModuleProvisioningException : Exception
{
    /// <summary>Enabling <see cref="ModuleId"/> failed because its provisioning contributor threw. Same value as <see cref="ModuleErrorCodes.ProvisioningFailed"/>.</summary>
    public const string ProvisioningFailed = ModuleErrorCodes.ProvisioningFailed;

    public ModuleProvisioningException(string moduleId, string contributor, Exception innerException)
        : base($"Module '{moduleId}' could not be enabled: provisioning by {contributor} failed ({innerException?.Message}).", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributor);
        ArgumentNullException.ThrowIfNull(innerException);

        ModuleId = moduleId;
        Contributor = contributor;
    }

    /// <summary>Always <see cref="ProvisioningFailed"/>; shaped like <see cref="Aonik.SharedKernel.Modules.ModuleDependencyException.Code"/> for the HTTP mapping.</summary>
    public string Code => ProvisioningFailed;

    /// <summary>The module whose provisioning failed. Its hard dependencies may already have been provisioned (idempotently) in the same request.</summary>
    public string ModuleId { get; }

    /// <summary>The contributor type that threw.</summary>
    public string Contributor { get; }
}
