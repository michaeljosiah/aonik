namespace Aonik.SharedKernel.Modules;

/// <summary>
/// The typed error codes clients see when module enablement rejects a request (Spec 097 §4, §9, §11).
/// </summary>
public static class ModuleErrorCodes
{
    /// <summary>403: the request targets a module the tenant has switched off.</summary>
    public const string Disabled = "module.disabled";

    /// <summary>409: enabling a module whose hard dependency is (or would be) off.</summary>
    public const string DependencyMissing = "module.dependency_missing";

    /// <summary>409: disabling a module that an enabled module still hard-depends on.</summary>
    public const string DependentsEnabled = "module.dependents_enabled";
}
