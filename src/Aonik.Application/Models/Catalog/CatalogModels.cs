namespace Aonik.Application.Models.Catalog;

public record CatalogCountryItem(
    string CountryCode,
    string Name);

public record CatalogCountryResponse(
    List<CatalogCountryItem> Countries);

public record CatalogBillerCategoryItem(
    Guid CategoryId,
    string Name,
    string? Description,
    string? IconUrl,
    string CountryCode);

public record CatalogBillerCategoryResponse(
    List<CatalogBillerCategoryItem> Categories);

public record CatalogBillerSummaryItem(
    Guid BillerId,
    string Name,
    string? LogoUrl,
    string CountryCode,
    Guid CategoryId,
    Guid? CorrespondentPartnerId,
    bool IsActive,
    bool IsFeatured);

public record CatalogBillerResponse(
    List<CatalogBillerSummaryItem> Billers,
    CatalogPaginationMetadata Pagination);

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

public record CatalogBillerServiceItem(
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
    List<CatalogBillerServiceItem> Services);

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
    List<CatalogServiceField> Fields,
    CatalogServiceValidation? Validation);

public record CatalogServiceField(
    string Key,
    string Label,
    string FieldType,
    bool Required,
    int? MinLength,
    int? MaxLength,
    string? Mask,
    string? Placeholder,
    List<CatalogServiceFieldOption>? Options);

public record CatalogServiceFieldOption(
    string Value,
    string Label);

public record CatalogServiceValidation(
    string? ValidationEndpoint,
    string? ValidationMode);

public record CatalogPaginationMetadata(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

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
