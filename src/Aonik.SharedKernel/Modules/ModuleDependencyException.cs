namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Thrown when a module toggle violates the hard-dependency graph (Spec 097 §4 "Dependencies"): the
/// request is rejected with the missing or dependent modules listed, never cascaded silently. The
/// HTTP layer maps it to <c>409</c> with <see cref="Code"/> as the typed error code.
/// </summary>
public sealed class ModuleDependencyException : Exception
{
    /// <summary>Enabling <see cref="ModuleId"/> requires <see cref="RelatedModuleIds"/>, which are off.</summary>
    public const string DependencyMissing = ModuleErrorCodes.DependencyMissing;

    /// <summary>Disabling <see cref="ModuleId"/> is blocked because <see cref="RelatedModuleIds"/> still depend on it.</summary>
    public const string DependentsEnabled = ModuleErrorCodes.DependentsEnabled;

    public ModuleDependencyException(string code, string moduleId, IReadOnlyList<string> relatedModuleIds)
        : base(BuildMessage(code, moduleId, relatedModuleIds))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(relatedModuleIds);

        Code = code;
        ModuleId = moduleId;
        RelatedModuleIds = relatedModuleIds;
    }

    /// <summary>One of <see cref="DependencyMissing"/> or <see cref="DependentsEnabled"/>.</summary>
    public string Code { get; }

    /// <summary>The module whose toggle was rejected.</summary>
    public string ModuleId { get; }

    /// <summary>The modules that caused the rejection: missing dependencies, or enabled dependents.</summary>
    public IReadOnlyList<string> RelatedModuleIds { get; }

    private static string BuildMessage(string code, string moduleId, IReadOnlyList<string> relatedModuleIds)
    {
        var related = string.Join(", ", relatedModuleIds ?? []);
        return code switch
        {
            DependencyMissing => $"Module '{moduleId}' cannot be enabled: it requires {related}.",
            DependentsEnabled => $"Module '{moduleId}' cannot be disabled: {related} still depend on it.",
            _ => $"Module '{moduleId}' toggle rejected ({code}): {related}.",
        };
    }
}
