using System.Reflection;

namespace Aonik.SharedKernel.Modules;

/// <summary>
/// The static, code-defined module catalogue (Spec 097 §5): the single source of truth for module
/// ids, display names, core status and the dependency graph. Lives in SharedKernel so every host,
/// module and the CLI can read it without a back-pointing dependency on Platform.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue is validated once at startup (<see cref="Validate()"/>, called from the Platform
/// composition root) and again by a unit test, so an invalid graph — an unknown dependency, a cycle,
/// or a core module resting on a non-core one — fails the build and the boot rather than a request.
/// </para>
/// <para>
/// <see cref="ResolveEnabled(IReadOnlyDictionary{string, bool})"/> is the pure resolution function
/// behind <see cref="IModuleEnablementReader"/>: it has no I/O and no caching, which is what makes
/// the reader's behaviour provable in isolation.
/// </para>
/// </remarks>
public static class ModuleCatalog
{
    private static readonly IReadOnlyList<ModuleDescriptor> Descriptors =
    [
        new(
            ModuleIds.Platform,
            "Platform",
            "Identity, tenancy, parties, compliance, notifications and settings.",
            IsCore: true,
            DependsOn: [],
            SoftDependsOn: []),
        new(
            ModuleIds.Ordering,
            "Ordering",
            "The generic order spine that domain modules build their order types on.",
            IsCore: true,
            DependsOn: [],
            SoftDependsOn: []),
        new(
            ModuleIds.Ai,
            "AI",
            "Model routing, prompts, policies, usage and execution records.",
            IsCore: true,
            DependsOn: [],
            SoftDependsOn: []),
        new(
            ModuleIds.Agents,
            "Agents",
            "Domain agents, orchestration, approvals and the run queue.",
            IsCore: true,
            DependsOn: [ModuleIds.Ai],
            SoftDependsOn: []),
        new(
            ModuleIds.Finance,
            "Finance",
            "Ledger, payments, billing, pricing, partners and cross-border money movement.",
            IsCore: false,
            DependsOn: [ModuleIds.Ordering],
            SoftDependsOn: [ModuleIds.Agents]),
        new(
            ModuleIds.Commerce,
            "Commerce",
            "Products, catalogue, inventory, storefront and maker operations.",
            IsCore: false,
            DependsOn: [ModuleIds.Ordering, ModuleIds.Finance],
            SoftDependsOn: [ModuleIds.Agents]),
        new(
            ModuleIds.Subscriptions,
            "Subscriptions",
            "Plans, entitlements and recurring billing.",
            IsCore: false,
            DependsOn: [ModuleIds.Ordering, ModuleIds.Finance],
            SoftDependsOn: [ModuleIds.Groups]),
        new(
            ModuleIds.Groups,
            "Groups",
            "Groups, membership and sharing across the platform.",
            IsCore: false,
            DependsOn: [],
            SoftDependsOn: []),
        new(
            ModuleIds.Workspaces,
            "Workspaces",
            "Content-addressed workspaces with revisions, quotas and sync.",
            IsCore: false,
            DependsOn: [ModuleIds.Groups, ModuleIds.Subscriptions],
            SoftDependsOn: []),
        new(
            ModuleIds.PersonalFinance,
            "Personal Finance",
            "Households, personal accounts, transactions, budgets, goals and insights.",
            IsCore: false,
            DependsOn: [],
            SoftDependsOn: [ModuleIds.Finance, ModuleIds.Groups, ModuleIds.Documents, ModuleIds.Agents]),
        new(
            ModuleIds.Voice,
            "Voice",
            "Realtime voice sessions and speech provider settings.",
            IsCore: false,
            DependsOn: [ModuleIds.Ai, ModuleIds.Agents],
            SoftDependsOn: []),
        new(
            ModuleIds.Documents,
            "Documents",
            "Document storage, linking, extraction and retrieval indexing.",
            IsCore: false,
            DependsOn: [],
            SoftDependsOn: [ModuleIds.Ai]),
    ];

    private static readonly IReadOnlyDictionary<string, ModuleDescriptor> ById =
        Descriptors.ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);

    /// <summary>Every shipped module, in catalogue order (core modules first).</summary>
    public static IReadOnlyList<ModuleDescriptor> All => Descriptors;

    /// <summary>The ids of the modules that can never be disabled.</summary>
    public static IReadOnlySet<string> CoreIds { get; } =
        Descriptors.Where(descriptor => descriptor.IsCore).Select(descriptor => descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Returns the descriptor for <paramref name="id"/>, or null when the id is not in the catalogue.</summary>
    public static ModuleDescriptor? TryGet(string id)
        => id is not null && ById.TryGetValue(id, out var descriptor) ? descriptor : null;

    /// <summary>Returns the descriptor for <paramref name="id"/>; throws when the id is not in the catalogue.</summary>
    /// <exception cref="KeyNotFoundException">The id is not a catalogue module.</exception>
    public static ModuleDescriptor Get(string id)
        => TryGet(id) ?? throw new KeyNotFoundException($"'{id}' is not a module in the catalogue.");

    /// <summary>True when <paramref name="id"/> names a catalogue module.</summary>
    public static bool IsKnown(string id) => id is not null && ById.ContainsKey(id);

    /// <summary>
    /// Validates the shipped catalogue. Throws <see cref="InvalidOperationException"/> on a
    /// duplicate id, an unknown dependency id (hard or soft), a cycle in the hard-dependency graph,
    /// or a core module that hard-depends on a non-core module.
    /// </summary>
    public static void Validate() => Validate(Descriptors);

    /// <summary>
    /// Validates an arbitrary descriptor list with the same rules as <see cref="Validate()"/>.
    /// Exposed so the rules themselves can be exercised against synthetic catalogues in tests.
    /// </summary>
    public static void Validate(IReadOnlyList<ModuleDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var byId = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Id))
                throw new InvalidOperationException("Module catalogue contains a descriptor with an empty id.");

            if (!byId.TryAdd(descriptor.Id, descriptor))
                throw new InvalidOperationException($"Module catalogue declares '{descriptor.Id}' more than once.");
        }

        foreach (var descriptor in descriptors)
        {
            foreach (var dependency in descriptor.DependsOn.Concat(descriptor.SoftDependsOn))
            {
                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidOperationException(
                        $"Module '{descriptor.Id}' depends on '{dependency}', which is not in the catalogue.");
                }
            }

            if (descriptor.IsCore)
            {
                var nonCore = descriptor.DependsOn.Where(dependency => !byId[dependency].IsCore).ToList();
                if (nonCore.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Core module '{descriptor.Id}' hard-depends on non-core module(s) {string.Join(", ", nonCore)}; " +
                        "a core module can never be off, so everything it requires must be core too.");
                }
            }
        }

        DetectHardDependencyCycle(byId);
    }

    /// <summary>
    /// The transitive hard-dependency closure of <paramref name="ids"/>, including the inputs
    /// themselves. Unknown ids are carried through unchanged so callers can validate them separately.
    /// </summary>
    public static IReadOnlySet<string> HardDependencyClosure(IEnumerable<string> ids)
        => HardDependencyClosure(ids, ById);

    /// <summary>
    /// Every module whose hard-dependency chain includes <paramref name="id"/> (transitive). Excludes
    /// <paramref name="id"/> itself. These are the modules that must be off before <paramref name="id"/> can be.
    /// </summary>
    public static IReadOnlySet<string> Dependents(string id)
        => Dependents(id, Descriptors);

    /// <summary>
    /// The pure resolution function (Spec 097 §7). Starting from the catalogue defaults, overlays the
    /// tenant's explicit rows, forces every core module on, then closes over hard dependencies so a
    /// module whose hard dependency resolved off is reported off even if its own row says on. The
    /// result is always dependency-consistent. Rows for ids not in the catalogue are ignored.
    /// </summary>
    public static IReadOnlySet<string> ResolveEnabled(IReadOnlyDictionary<string, bool> explicitRows)
        => ResolveEnabled(explicitRows, Descriptors);

    /// <summary>
    /// <see cref="ResolveEnabled(IReadOnlyDictionary{string, bool})"/> over an arbitrary descriptor
    /// list, for tests that need a synthetic catalogue.
    /// </summary>
    public static IReadOnlySet<string> ResolveEnabled(
        IReadOnlyDictionary<string, bool> explicitRows,
        IReadOnlyList<ModuleDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(explicitRows);
        ArgumentNullException.ThrowIfNull(descriptors);

        var byId = descriptors.ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);

        // 1. Catalogue defaults.
        var enabled = descriptors
            .Where(descriptor => descriptor.DefaultEnabled || descriptor.IsCore)
            .Select(descriptor => descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

        // 2. Overlay explicit rows — an explicit value wins over the default.
        foreach (var (moduleId, isEnabled) in explicitRows)
        {
            if (!byId.ContainsKey(moduleId))
                continue;

            if (isEnabled)
                enabled.Add(moduleId);
            else
                enabled.Remove(moduleId);
        }

        // 3. Force core modules on regardless of rows.
        foreach (var descriptor in descriptors.Where(descriptor => descriptor.IsCore))
            enabled.Add(descriptor.Id);

        // 4. Dependency closure: drop anything whose hard dependency is off, until stable.
        bool changed;
        do
        {
            changed = false;
            foreach (var descriptor in descriptors)
            {
                if (!enabled.Contains(descriptor.Id))
                    continue;

                if (descriptor.DependsOn.Any(dependency => !enabled.Contains(dependency)))
                {
                    enabled.Remove(descriptor.Id);
                    changed = true;
                }
            }
        }
        while (changed);

        return enabled;
    }

    /// <summary>
    /// Reads the module id declared on <paramref name="assembly"/> via <see cref="AonikModuleAttribute"/>;
    /// null when the assembly carries none (Api, Application, Infrastructure and SharedKernel never do).
    /// </summary>
    public static string? TryGetModuleId(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetCustomAttribute<AonikModuleAttribute>()?.ModuleId;
    }

    /// <summary>
    /// The module id of the assembly that declares <paramref name="type"/>; null when that assembly
    /// carries no <see cref="AonikModuleAttribute"/>.
    /// </summary>
    public static string? TryGetModuleId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return TryGetModuleId(type.Assembly);
    }

    private static HashSet<string> HardDependencyClosure(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, ModuleDescriptor> byId)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(ids);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!closure.Add(current))
                continue;

            if (!byId.TryGetValue(current, out var descriptor))
                continue;

            foreach (var dependency in descriptor.DependsOn)
                pending.Push(dependency);
        }

        return closure;
    }

    private static HashSet<string> Dependents(string id, IReadOnlyList<ModuleDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(id);

        var dependents = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(id);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var descriptor in descriptors)
            {
                if (descriptor.Id == id || !descriptor.DependsOn.Contains(current, StringComparer.Ordinal))
                    continue;

                if (dependents.Add(descriptor.Id))
                    pending.Push(descriptor.Id);
            }
        }

        return dependents;
    }

    private static void DetectHardDependencyCycle(IReadOnlyDictionary<string, ModuleDescriptor> byId)
    {
        // Iterative DFS with three colours: 0 = unvisited, 1 = on the current path, 2 = finished.
        var state = byId.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);

        foreach (var root in byId.Keys)
        {
            if (state[root] != 0)
                continue;

            var path = new Stack<(string Id, IEnumerator<string> Edges)>();
            state[root] = 1;
            path.Push((root, byId[root].DependsOn.GetEnumerator()));

            while (path.Count > 0)
            {
                var (current, edges) = path.Peek();
                if (edges.MoveNext())
                {
                    var next = edges.Current;
                    switch (state[next])
                    {
                        case 1:
                            var cycle = path.Select(frame => frame.Id).Reverse().SkipWhile(id => id != next).Append(next);
                            throw new InvalidOperationException(
                                $"Module catalogue hard-dependency graph contains a cycle: {string.Join(" -> ", cycle)}.");
                        case 0:
                            state[next] = 1;
                            path.Push((next, byId[next].DependsOn.GetEnumerator()));
                            break;
                    }
                }
                else
                {
                    state[current] = 2;
                    path.Pop();
                }
            }
        }
    }
}
