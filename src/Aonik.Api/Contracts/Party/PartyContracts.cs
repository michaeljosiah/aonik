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
