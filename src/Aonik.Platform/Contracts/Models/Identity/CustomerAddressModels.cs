using System.Text.Json.Serialization;

namespace Aonik.Platform.Contracts.Models.Identity;

public record CustomerAddressDto(Guid Id, string Type, string Line1, string? Line2, string? Line3,
    string City, string? State, string Postcode, string Country, bool IsDefault);

public record CustomerAddressBookDto(IReadOnlyList<CustomerAddressDto> Addresses, Guid? DefaultAddressId, string Version);

/// <summary>Full address replacement. The version is the observed parent address-book version.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CustomerAddressWrite(string Type, string Line1, string? Line2, string? Line3,
    string City, string? State, string Postcode, string Country, string? ExpectedVersion = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CustomerAddressVersionRequest(string? ExpectedVersion = null);
