namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Thrown when a request reaches a module the tenant has switched off. The HTTP layer maps it to
/// <c>403 { error, code: "module.disabled", moduleId }</c> (Spec 097 §11) — 403 rather than 404 so an
/// operator can tell "disabled" from "does not exist".
/// </summary>
public sealed class ModuleDisabledException : Exception
{
    public ModuleDisabledException(string moduleId)
        : base($"Module '{moduleId}' is disabled for this tenant.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ModuleId = moduleId;
    }

    /// <summary>The catalogue id of the disabled module.</summary>
    public string ModuleId { get; }

    /// <summary>Always <see cref="ModuleErrorCodes.Disabled"/>.</summary>
    public string Code => ModuleErrorCodes.Disabled;
}
