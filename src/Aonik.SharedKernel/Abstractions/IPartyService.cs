namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module party service contract. Implemented by Platform, consumed by Finance.
/// </summary>
public interface IPartyService
{
    Task<PartyResponse> CreatePartyAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default);

    Task<PartyResponse?> GetPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<RelatedPartyResponse> CreateRelatedPartyAsync(
        CreateRelatedPartyRequest request,
        CancellationToken cancellationToken = default);

    Task<PartyRelationshipResponse> CreateRelationshipAsync(
        CreatePartyRelationshipRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyRelationshipResponse>> GetRelationshipsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently assigns a business role (see <see cref="PartyRoleCodes"/>) to a party within a
    /// context (e.g. role <c>Beneficiary</c> in context <c>Customer</c> = the owning customer's id).
    /// A no-op if the same (party, role, context) assignment already exists, so callers can invoke it
    /// on every save without creating duplicates.
    /// </summary>
    Task AssignPartyRoleAsync(
        Guid partyId,
        string role,
        string contextType,
        Guid contextId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the profile-photo URLs (original + thumbnails) for a set of parties in one round-trip.
    /// Parties without a person profile or photo come back with null URLs. Lets a consumer (e.g. the
    /// recipient projection) enrich a list with photos without widening the lean <see cref="PartyResponse"/>.
    /// </summary>
    Task<IReadOnlyList<PartyPhotoUrls>> GetPartyPhotosAsync(
        IReadOnlyCollection<Guid> partyIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a party's profile photo (original + thumbnails via the profile-photo store) and persists
    /// the resulting URLs on the party's person profile. The party must have a person profile
    /// (Person/Individual) — a business party has no photo. Returns the stored URLs.
    /// </summary>
    Task<PartyPhotoUrls> SetPartyPhotoAsync(
        Guid partyId,
        string contentType,
        Stream photo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the mutable fields of a directed relationship edge — its type code, notes, and/or active
    /// flag. A null argument leaves that field unchanged. Returns false when no edge with that id exists
    /// in the current tenant. Setting <paramref name="isActive"/> to false is how a recipient is
    /// soft-removed: the edge is deactivated without deleting the party or any history.
    /// </summary>
    Task<bool> UpdateRelationshipAsync(
        Guid relationshipId,
        string? relationshipTypeCode = null,
        string? notes = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Profile-photo URLs for a party: original plus the three thumbnail sizes.</summary>
public record PartyPhotoUrls(
    Guid PartyId,
    string? PhotoUrl,
    string? PhotoUrlMedium,
    string? PhotoUrlSmall,
    string? PhotoUrlTiny);

public record CreatePartyRequest(
    string DisplayName,
    string PartyType,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? CountryCode);

public record PartyResponse(
    Guid PartyId,
    string DisplayName,
    string PartyType,
    string Status);

public record CreatePartyRelationshipRequest(
    Guid FromPartyId,
    Guid ToPartyId,
    string RelationshipTypeCode,
    string? Notes);

public record CreateRelatedPartyRequest(
    Guid CustomerPartyId,
    string RelationshipTypeCode,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? CountryCode,
    string? Notes);

public record PartyRelationshipResponse(
    Guid RelationshipId,
    Guid FromPartyId,
    string FromPartyName,
    Guid ToPartyId,
    string ToPartyName,
    string RelationshipTypeCode,
    string RelationshipTypeName,
    bool IsActive);

public record RelatedPartyResponse(
    PartyResponse Party,
    PartyRelationshipResponse Relationship);
