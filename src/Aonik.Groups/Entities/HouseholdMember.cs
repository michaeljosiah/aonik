using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class HouseholdMember : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid HouseholdId { get; set; }
    /// <summary>
    /// The member, as a PARTY (Spec 086 / ADR-015). This is the decision the whole extraction turns
    /// on: a child has no login, so a membership keyed on an authenticated principal cannot
    /// represent them at all.
    ///
    /// Nullable through the transition only — populated by the P3 backfill from
    /// <see cref="UserId"/>, then made required once every environment is confirmed.
    /// </summary>
    public Guid? PartyId { get; set; }

    /// <summary>
    /// The member's user, where they have one. Nullable from Spec 086: a party-only member has
    /// none. Retained alongside <see cref="PartyId"/> through the dual-write window, because the
    /// deployed readers still use it — see the P3/P5 phasing.
    /// </summary>
    public Guid? UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = string.Empty;
    public string InvitationStatus { get; set; } = string.Empty;
    public Guid? InvitedByUserId { get; set; }
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
