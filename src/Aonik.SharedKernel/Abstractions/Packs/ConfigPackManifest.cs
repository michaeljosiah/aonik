namespace Aonik.SharedKernel.Abstractions.Packs;

/// <summary>
/// A business-type configuration pack (Spec 065) — the declarative default configuration for a
/// business type, shipped as an embedded JSON manifest and applied at provision time. This is the
/// <em>config</em> layer (identity + behaviour); it is data, not code, so a product name appearing
/// here is configuration, exactly where ADR-013 says it belongs. Lives in SharedKernel so any module
/// (and the CLI) can read a manifest without a back-pointing dependency on Platform.
/// </summary>
public sealed record ConfigPackManifest
{
    /// <summary>The business type this pack configures (open string; matches the tenant's BusinessType).</summary>
    public string BusinessType { get; init; } = BusinessTypes.Base;

    /// <summary>Monotonic pack version, recorded on the tenant as <c>AppliedPackVersion</c>.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Human label for the pack (not a platform symbol).</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// The catalogue module ids (<c>Aonik.SharedKernel.Modules.ModuleIds</c>) this business type enables
    /// (Spec 097 §13). Validated against the catalogue on load — an unknown id fails the pack. A pack that
    /// declares modules is authoritative for a new tenant: the declared modules, their transitive hard
    /// dependencies and the core modules are on, everything else is off. An empty list leaves the
    /// catalogue defaults (every module on).
    /// </summary>
    public List<string> Modules { get; init; } = new();

    /// <summary>Tenant-scoped settings / feature flags to apply (key → value). Applied additive-only.</summary>
    public Dictionary<string, string> Settings { get; init; } = new();

    /// <summary>Per-agent overrides (persona, toolset, model) to apply for the tenant.</summary>
    public List<ConfigPackAgent> Agents { get; init; } = new();

    /// <summary>Tenant reference-data to seed (e.g. units of measure, categories).</summary>
    public List<ConfigPackReferenceData> ReferenceData { get; init; } = new();
}

/// <summary>An agent override carried by a pack — populates the tenant's <c>Agent</c> config row.</summary>
public sealed record ConfigPackAgent
{
    /// <summary>The code-defined agent name to override (e.g. <c>personal-finance-agent</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The persona / system prompt for this tenant. Null leaves the platform default unchanged.</summary>
    public string? InstructionsText { get; init; }

    /// <summary>The enabled tool names for this tenant. Null leaves the default toolset unchanged.</summary>
    public List<string>? Toolset { get; init; }

    /// <summary>The model to pin. Null uses platform default routing.</summary>
    public Guid? ModelId { get; init; }
}

/// <summary>A reference-data group carried by a pack (e.g. <c>Type = "unit_of_measure"</c>).</summary>
public sealed record ConfigPackReferenceData
{
    public string Type { get; init; } = string.Empty;
    public List<ConfigPackReferenceDataItem> Items { get; init; } = new();
}

public sealed record ConfigPackReferenceDataItem
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
