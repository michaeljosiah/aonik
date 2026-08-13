namespace Aonik.Platform.Entities.Party;

/// <summary>
/// Guardianship is stored as a <see cref="PartyRelationship"/> with
/// <see cref="PartyRelationshipTypes.Guardian"/> — from guardian, to child. These helpers exist so
/// the direction and the semantics are expressed once rather than re-derived at each call site.
/// </summary>
public static class GuardianRelationship
{
    /// <summary>
    /// True when the relationship grants authority over <paramref name="childPartyId"/>.
    ///
    /// <para>
    /// Note what this deliberately does NOT do: a <see cref="PartyRelationshipTypes.Mother"/> or
    /// <see cref="PartyRelationshipTypes.Child"/> edge is never treated as guardianship. Inferring
    /// authority from kinship gets real families wrong, and gets them wrong in the direction of
    /// granting access to someone who should not have it (Spec 095 §7).
    /// </para>
    /// </summary>
    public static bool GrantsAuthorityOver(PartyRelationship relationship, Guid childPartyId)
        => relationship.IsActive
            && relationship.ToPartyId == childPartyId
            && string.Equals(
                relationship.RelationshipTypeCode,
                PartyRelationshipTypes.Guardian,
                StringComparison.OrdinalIgnoreCase);
}
