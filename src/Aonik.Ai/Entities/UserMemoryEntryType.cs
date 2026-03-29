namespace Aonik.Ai.Entities;

/// <summary>
/// Discriminator for the type of user memory entry.
/// </summary>
public enum UserMemoryEntryType
{
    /// <summary>Identity facts about the user (e.g., corridor countries, household context).</summary>
    Identity = 1,

    /// <summary>Explicit preferences stated by the user (e.g., reminder timing, agent behaviour).</summary>
    Preference = 2,

    /// <summary>User-initiated corrections to AI-inferred or system-derived data.</summary>
    Correction = 3,

    /// <summary>General facts learned about the user (e.g., income patterns).</summary>
    Fact = 4
}
