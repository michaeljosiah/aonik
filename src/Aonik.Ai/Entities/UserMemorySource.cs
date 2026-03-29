namespace Aonik.Ai.Entities;

/// <summary>
/// Identifies how a user memory entry was created.
/// </summary>
public enum UserMemorySource
{
    /// <summary>The user explicitly stated this value.</summary>
    UserStated = 1,

    /// <summary>An AI run inferred this value from behaviour or conversation.</summary>
    AiInferred = 2,

    /// <summary>The system derived this value from domain data (e.g., account sync).</summary>
    SystemDerived = 3
}
