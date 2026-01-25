namespace Aonik.Api.Contracts.Catalog;

public record CatalogCountryItemResponse(
    string CountryCode,
    string Name);

public record CatalogCountryResponse(
    List<CatalogCountryItemResponse> Countries);

public record CatalogBillerCategoryItemResponse(
    Guid CategoryId,
    string Name,
    string? Description,
    string? IconUrl,
    string CountryCode);

public record CatalogBillerCategoryResponse(
    List<CatalogBillerCategoryItemResponse> Categories);

public record CatalogBillerSummaryItemResponse(
    Guid BillerId,
    string Name,
    string? LogoUrl,
    string CountryCode,
    Guid CategoryId,
    Guid? CorrespondentPartnerId,
    bool IsActive,
    bool IsFeatured);

public record CatalogBillerResponse(
    List<CatalogBillerSummaryItemResponse> Billers,
    CatalogPaginationMetadataResponse Pagination);

public record CatalogPaginationMetadataResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record CatalogBillerDetailResponse(
    Guid BillerId,
    string Name,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    string? SupportPhone,
    string? SupportEmail,
    string CountryCode,
    Guid CategoryId,
    Guid? CorrespondentPartnerId,
    bool IsActive,
    int ServiceCount);

public record CatalogBillerServiceItemResponse(
    Guid ServiceId,
    string ServiceCode,
    string Name,
    string Type,
    string Currency,
    decimal? MinAmount,
    decimal? MaxAmount,
    bool SupportsPartialPayment,
    bool RequiresValidation,
    bool IsActive);

public record CatalogBillerServiceResponse(
    List<CatalogBillerServiceItemResponse> Services);

public record CatalogBillerServiceDetailResponse(
    Guid ServiceId,
    string ServiceCode,
    string Name,
    string Type,
    string Currency,
    decimal? MinAmount,
    decimal? MaxAmount,
    bool SupportsPartialPayment,
    bool RequiresValidation,
    List<CatalogServiceFieldResponse> Fields,
    CatalogServiceValidationResponse? Validation);

public record CatalogServiceFieldResponse(
    string Key,
    string Label,
    string FieldType,
    bool Required,
    int? MinLength,
    int? MaxLength,
    string? Mask,
    string? Placeholder,
    List<CatalogServiceFieldOptionResponse>? Options);

public record CatalogServiceFieldOptionResponse(
    string Value,
    string Label);

public record CatalogServiceValidationResponse(
    string? ValidationEndpoint,
    string? ValidationMode);

public record CatalogServiceFieldValidationRequest(
    Dictionary<string, string> FieldValues);

public record CatalogServiceFieldValidationResponse(
    bool IsValid,
    DateTimeOffset ValidatedAt,
    string? ErrorCode,
    string? ErrorMessage,
    string? AccountHolderName,
    Dictionary<string, string>? AdditionalInfo);

public record CatalogBillerListRequest(
    string? CountryCode,
    Guid? CategoryId,
    string? Search,
    int Page,
    int PageSize);

public record CatalogCategoryListRequest(
    string? CountryCode);

public record CatalogCountryListRequest(
    bool OnlyServiceCountries);
