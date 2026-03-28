namespace Aonik.Platform.Contracts.Models.Customers;

public record ListCustomersRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? PartyType = null,
    string? Search = null
);

public record CustomerListItem(
    Guid PartyId,
    string DisplayName,
    string PartyType,
    string Status,
    string? PrimaryEmail,
    string? PrimaryPhone,
    string? PhotoUrlTiny,
    string? VerificationStatus,
    DateTime CreatedAt
);

public record PartyConsentDetail(
    Guid ConsentId,
    string ConsentType,
    DateTime GrantedAt,
    DateTime? RevokedAt
);

public record ExternalAccountDetail(
    Guid ExternalAccountId,
    string ExternalAccountType,
    string MaskedIdentifier,
    string? ProviderRef,
    string VerificationStatus,
    string? Currency,
    string? Country,
    string MetadataJson
);

public record PartyRoleAssignmentDetail(
    Guid RoleAssignmentId,
    string Role,
    string ContextType,
    Guid ContextId
);

public record PartyRelationshipDetail(
    Guid RelationshipId,
    Guid FromPartyId,
    Guid ToPartyId,
    string RelationshipTypeCode,
    bool IsActive,
    string? Notes
);

public record CurrencyAmount(
    string Currency,
    decimal Amount
);

public record CustomerStats(
    Guid PartyId,
    int TotalOrders,
    IReadOnlyList<CurrencyAmount> TotalPaidByCurrency,
    IReadOnlyList<CurrencyAmount> OutstandingByCurrency,
    DateTime? LastActivityAt
);

public record CustomerDetail(
    Guid PartyId,
    string DisplayName,
    string PartyType,
    string Status,
    string? CustomerTierCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Aonik.Platform.Contracts.Models.Identity.PersonProfileDetail? PersonProfile,
    Aonik.Platform.Contracts.Models.Identity.BusinessProfileDetail? BusinessProfile,
    List<Aonik.Platform.Contracts.Models.Identity.PartyContactDetail> Contacts,
    List<Aonik.Platform.Contracts.Models.Identity.PartyAddressDetail> Addresses,
    List<PartyConsentDetail> Consents,
    List<ExternalAccountDetail> ExternalAccounts,
    List<PartyRoleAssignmentDetail> RoleAssignments,
    List<PartyRelationshipDetail> Relationships
);

public record CreateCustomerContactRequest(
    string Type,
    string Value,
    bool IsPrimary
);

public record CreateCustomerAddressRequest(
    string Type,
    string Line1,
    string? Line2,
    string? Line3,
    string City,
    string? State,
    string Postcode,
    string Country
);

public record CreateCustomerRequest(
    string DisplayName,
    string PartyType,
    string Status,
    string? CustomerTierCode,
    string? Title,
    string? FirstName,
    string? LastName,
    DateTime? Dob,
    string? Nationality,
    string? Occupation,
    string? CountryCode,
    string? RegistrationNumber,
    string? IncorporationCountry,
    string? Industry,
    List<CreateCustomerContactRequest> Contacts,
    List<CreateCustomerAddressRequest> Addresses
);

public record CreateCustomerResponse(
    Guid PartyId,
    string DisplayName,
    string PartyType,
    string Status,
    DateTime CreatedAt
);
