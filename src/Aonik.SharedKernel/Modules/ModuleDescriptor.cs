namespace Aonik.SharedKernel.Modules;

/// <summary>
/// One entry of the <see cref="ModuleCatalog"/> (Spec 097 §5): a module's canonical id, display
/// metadata, whether it can be switched off per tenant, and its declared dependency edges.
/// </summary>
/// <param name="Id">Canonical kebab-case id, e.g. <c>personal-finance</c>. See <see cref="ModuleIds"/>.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">One-line description.</param>
/// <param name="IsCore">Core modules cannot be disabled for any tenant.</param>
/// <param name="DependsOn">
/// Hard dependencies: the module cannot function without them. Enforced on toggle and closed over
/// during resolution — a module whose hard dependency resolves off is itself reported off.
/// </param>
/// <param name="SoftDependsOn">Soft dependencies: documented; the module degrades gracefully without them.</param>
/// <param name="DefaultEnabled">The state a tenant with no explicit row gets. Every shipped module defaults to on.</param>
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    string Description,
    bool IsCore,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> SoftDependsOn,
    bool DefaultEnabled = true);
