namespace Aonik.Api.Contracts.Party;

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

public record CreateRelatedPartyRequest(
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
