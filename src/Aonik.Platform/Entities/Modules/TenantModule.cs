using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Modules;

/// <summary>
/// One tenant's explicit enablement state for one catalogue module (Spec 097 §6). An absent row means
/// the catalogue default; a row is written by a config pack at provisioning or by a host admin
/// afterwards. <c>RowVersion</c> (inherited from <see cref="AuditableEntity"/>) provides optimistic
/// concurrency on concurrent toggles.
/// </summary>
public class TenantModule : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Catalogue id; validated against <c>ModuleCatalog</c> in the service, never free text.</summary>
    public string ModuleId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>How the row came to exist — one of <see cref="TenantModuleSource"/>.</summary>
    public string Source { get; set; } = TenantModuleSource.Explicit;

    /// <summary>Free-text reason for the last change, audited.</summary>
    public string? Reason { get; set; }
}

/// <summary>Known values for <see cref="TenantModule.Source"/>.</summary>
public static class TenantModuleSource
{
    /// <summary>Written by the tenant's config pack at provisioning.</summary>
    public const string Pack = "pack";

    /// <summary>Written by a host admin toggle.</summary>
    public const string Explicit = "explicit";
}
