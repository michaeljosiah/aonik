namespace Aonik.Api.Contracts.Identity;

public record UpdateCustomerProfileRequest(
    string? DisplayName,
    string? Email,
    string? Phone,
    CustomerAddressRequest? Address);

public record CustomerAddressRequest(
    string Line1,
    string? Line2,
    string? Line3,
    string City,
    string? State,
    string Postcode,
    string Country);

public record CustomerProfileResponse(
    Guid PartyId,
    string DisplayName,
    string? Email,
    string? Phone,
    CustomerAddressResponse? Address);

public record CustomerAddressResponse(
    string Line1,
    string? Line2,
    string? Line3,
    string City,
    string? State,
    string Postcode,
    string Country);
