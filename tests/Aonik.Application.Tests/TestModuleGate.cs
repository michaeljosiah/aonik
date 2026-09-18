using Aonik.SharedKernel.Modules;

// Spec 097 test double in the global namespace, like the Spec 042 doubles, so every service test can
// construct a gated service without a using directive.

/// <summary>
/// An <see cref="IModuleGate"/> whose answer is fixed per test: <see cref="AllowAll"/> for the ordinary
/// path, or <see cref="Denying"/> to prove a processor refuses a tenant with the module off and mutates
/// nothing. Records every call so a test can assert the gate was consulted with the tenant the
/// processor <em>resolved</em>, not the ambient one.
/// </summary>
internal sealed class TestModuleGate : IModuleGate
{
    private readonly ISet<string> _disabledModuleIds;

    private TestModuleGate(IEnumerable<string> disabledModuleIds)
    {
        _disabledModuleIds = disabledModuleIds.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Every module is on — the catalogue default.</summary>
    public static TestModuleGate AllowAll => new(Array.Empty<string>());

    /// <summary>The named modules are off for every tenant.</summary>
    public static TestModuleGate Denying(params string[] disabledModuleIds) => new(disabledModuleIds);

    public List<(Guid TenantId, string ModuleId)> Calls { get; } = new();

    public Task EnsureEnabledAsync(Guid tenantId, string moduleId, CancellationToken cancellationToken = default)
    {
        Calls.Add((tenantId, moduleId));
        return _disabledModuleIds.Contains(moduleId)
            ? throw new ModuleDisabledException(moduleId)
            : Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync(Guid tenantId, string moduleId, CancellationToken cancellationToken = default)
    {
        Calls.Add((tenantId, moduleId));
        return Task.FromResult(!_disabledModuleIds.Contains(moduleId));
    }
}
