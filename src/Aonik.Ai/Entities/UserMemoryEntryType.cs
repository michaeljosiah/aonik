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
    Fact = 4,

    /// <summary>
    /// A decision rationale (Spec 041): why a past choice was made, under which conditions it
    /// applies, and what invalidates it. Stored like any other entry (the column is a string,
    /// so this value needs no migration); the decision type and subject grain live in the dot-key
    /// (e.g. <c>decision.remittance-routing.payee.{id}</c>) and the structured rationale in
    /// <c>ValueJson</c>. Recall applies condition relevance before surfacing it as a caveated prior.
    /// </summary>
    Rationale = 5
}
