namespace Aonik.SharedKernel.Persistence;

/// <summary>
/// Canonical short table prefixes for module-owned tables in a shared database.
/// </summary>
public static class ModuleTablePrefixes
{
    public const string Default = "Ank";

    public const string Platform = Default;
    public const string Finance = Default;
    public const string Ai = Default;
    public const string Agents = Default;
    public const string Voice = Default;
}
