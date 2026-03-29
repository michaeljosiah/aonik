using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

/// <summary>
/// A single key-value entry representing something the AI has learned about a user.
/// Combines identity facts, preferences, corrections, and general facts into one
/// table with a type discriminator. Superseded entries form an audit chain via
/// <see cref="SupersededById"/>.
/// </summary>
public class UserMemoryEntry : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public UserMemoryEntryType EntryType { get; set; }

    /// <summary>
    /// Namespaced key, e.g. "communication.reminder_time", "corridor.home_country".
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// JSON-encoded value. Keeps the table schema-agnostic.
    /// </summary>
    public string ValueJson { get; set; } = string.Empty;

    /// <summary>
    /// 1.0 = user-stated, less than 1.0 = AI-inferred. Used with confidence decay at read time.
    /// </summary>
    public decimal Confidence { get; set; } = 1.0m;

    public UserMemorySource Source { get; set; }

    /// <summary>
    /// The AI run that produced this entry (for inferred entries). Null for user-stated entries.
    /// </summary>
    public Guid? AiRunId { get; set; }

    /// <summary>
    /// Points to the entry that replaced this one. Null = current.
    /// Query for current entries: WHERE SupersededById IS NULL.
    /// </summary>
    public Guid? SupersededById { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Updated when the user re-confirms. Used for staleness/confidence decay checks.
    /// </summary>
    public DateTime LastConfirmedAt { get; set; }
}
