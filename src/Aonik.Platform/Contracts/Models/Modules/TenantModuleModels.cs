namespace Aonik.Platform.Contracts.Models.Modules;

/// <summary>One requested change to a tenant's module state (Spec 097 §9).</summary>
/// <param name="ModuleId">Catalogue id; must be known and non-core.</param>
/// <param name="IsEnabled">The desired state.</param>
/// <param name="Reason">Free-text reason, audited and stored on the row.</param>
public record TenantModuleToggle(
    string ModuleId,
    bool IsEnabled,
    string? Reason = null);

/// <summary>
/// One catalogue module projected with a tenant's state (Spec 097 §9). Every catalogue module is
/// reported whether or not a row exists; <see cref="Source"/> explains where the state came from.
/// </summary>
/// <param name="Source">One of <see cref="TenantModuleStateSource"/>.</param>
/// <param name="UpdatedAt">The row's last change (falls back to its creation); null without a row.</param>
public record TenantModuleState(
    string ModuleId,
    string Name,
    string Description,
    bool IsCore,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> SoftDependsOn,
    bool IsEnabled,
    string Source,
    string? Reason,
    DateTime? UpdatedAt,
    Guid? UpdatedBy);

/// <summary>The full, dependency-consistent module state for one tenant.</summary>
public record TenantModuleList(
    Guid TenantId,
    IReadOnlyList<TenantModuleState> Modules);

/// <summary>Known values for <see cref="TenantModuleState.Source"/>.</summary>
public static class TenantModuleStateSource
{
    /// <summary>A core module: always on, never backed by a row.</summary>
    public const string Core = "core";

    /// <summary>No row exists; the catalogue default applies.</summary>
    public const string Default = "default";

    /// <summary>The row was written by the tenant's config pack at provisioning.</summary>
    public const string Pack = "pack";

    /// <summary>The row was written by a host admin toggle.</summary>
    public const string Explicit = "explicit";
}
