namespace Aonik.Application.Models.Identity;

public record CustomerAddress(
    string Line1,
    string? Line2,
    string? Line3,
    string City,
    string? State,
    string Postcode,
    string Country);

public record CustomerProfile(
    Guid PartyId,
    string DisplayName,
    string? Email,
    string? Phone,
    CustomerAddress? Address);

public record CustomerProfileUpdateRequest(
    string? DisplayName,
    string? Email,
    string? Phone,
    CustomerAddress? Address);
