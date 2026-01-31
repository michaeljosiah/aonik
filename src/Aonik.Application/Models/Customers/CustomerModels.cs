namespace Aonik.Application.Models.Customers;

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

public record CustomerDetail(
    Guid PartyId,
    string DisplayName,
    string PartyType,
    string Status,
    string? CustomerTierCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Aonik.Application.Models.Identity.PersonProfileDetail? PersonProfile,
    Aonik.Application.Models.Identity.BusinessProfileDetail? BusinessProfile,
    List<Aonik.Application.Models.Identity.PartyContactDetail> Contacts,
    List<Aonik.Application.Models.Identity.PartyAddressDetail> Addresses,
    List<PartyConsentDetail> Consents,
    List<ExternalAccountDetail> ExternalAccounts,
    List<PartyRoleAssignmentDetail> RoleAssignments,
    List<PartyRelationshipDetail> Relationships
);
