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
}

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
