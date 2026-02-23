namespace Aonik.Finance.Contracts.Models.Partners;

public record ListPartnersRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? CountryCode = null,
    string? Search = null
);

public record PartnerListItem(
    Guid PartnerId,
    string Name,
    string Status,
    int BranchCount,
    int ConnectorCount,
    int ActiveRoutingRuleCount,
    int LinkedBillerCount,
    IReadOnlyList<string> CoverageCountries,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PartnerBranchItem(
    Guid BranchId,
    string Name,
    string Country,
    string City,
    string? MetadataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PartnerConnectorItem(
    Guid ConnectorId,
    string ConnectorType,
    string Status,
    string? CredentialsRef,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PartnerRoutingRuleItem(
    Guid RoutingRuleId,
    int Priority,
    bool IsActive,
    string? ConditionsJson,
    Guid? TargetConnectorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PartnerTransmissionItem(
    Guid TransmissionId,
    Guid ConnectorId,
    string? ConnectorType,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PartnerLinkedBillerItem(
    Guid BillerId,
    string Name,
    string CountryCode,
    bool IsActive,
    int ServiceCount
);

public record PartnerDetail(
    Guid PartnerId,
    string Name,
    string Status,
    string CapabilitiesJson,
    string OperatingHoursJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int BranchCount,
    int ConnectorCount,
    int ActiveRoutingRuleCount,
    int LinkedBillerCount,
    IReadOnlyList<PartnerBranchItem> Branches,
    IReadOnlyList<PartnerConnectorItem> Connectors,
    IReadOnlyList<PartnerRoutingRuleItem> RoutingRules,
    IReadOnlyList<PartnerTransmissionItem> RecentTransmissions,
    IReadOnlyList<PartnerLinkedBillerItem> LinkedBillers
);

public record CreatePartnerRequest(
    string Name,
    string? Status,
    string? CapabilitiesJson,
    string? OperatingHoursJson
);

public record UpdatePartnerRequest(
    string? Name,
    string? Status,
    string? CapabilitiesJson,
    string? OperatingHoursJson
);

public record CreatePartnerResponse(
    Guid PartnerId,
    string Name,
    string Status,
    DateTime CreatedAt
);
