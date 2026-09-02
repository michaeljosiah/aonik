namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Canonical kebab-case module ids (Spec 097 §5). This is the ONLY place a module id may be spelled
/// as a literal: backend assemblies declare theirs through <see cref="AonikModuleAttribute"/>,
/// config packs and the Admin UI manifest use the same strings, and <see cref="ModuleCatalog"/>
/// is the single source of truth for what each id means.
/// </summary>
public static class ModuleIds
{
    public const string Platform = "platform";
    public const string Ordering = "ordering";
    public const string Finance = "finance";
    public const string Commerce = "commerce";
    public const string Subscriptions = "subscriptions";
    public const string Groups = "groups";
    public const string Workspaces = "workspaces";
    public const string PersonalFinance = "personal-finance";
    public const string Ai = "ai";
    public const string Agents = "agents";
    public const string Voice = "voice";
    public const string Documents = "documents";
}
