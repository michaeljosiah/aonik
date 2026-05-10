using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Voice.Entities;

/// <summary>
/// Singleton-per-tenant Voice Mode active settings (spec 024 Phase C). At most one row per
/// tenant; the tenant id is the primary key. Maps to <c>AnkVoiceModeSettings</c> in <c>dbo</c>.
///
/// <para>
/// The runtime cutover (Phase C.2) wires <c>AonikVoicePipelineFactory</c> to read
/// <see cref="ActiveRecipeId"/> from this row instead of the legacy
/// <c>VoiceProviderSettings</c>; for now the row is written by the admin UI but not yet read at
/// the WSS layer.
/// </para>
/// </summary>
public sealed class VoiceModeSettingsEntity : AuditableEntity, ITenantScoped
{
    /// <summary>Tenant id is BOTH the primary key (one row per tenant) and the tenant scope.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Currently active recipe id. Built-ins use the <c>built-in:&lt;name&gt;</c> format; tenant
    /// recipes use the row Guid in N format. Null when no recipe is selected.
    /// </summary>
    public string? ActiveRecipeId { get; set; }

    /// <summary>Workspace-wide on/off switch. Default true on first-time creation.</summary>
    public bool Enabled { get; set; } = true;
}
