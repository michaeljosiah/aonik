namespace Aonik.Application.Models.Identity;

public record TenantResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Environment,
    string DefaultCurrency,
    string[] SupportedCountries,
    string[] SupportedCurrencies,
    string Status,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime? UpdatedAt,
    Guid? UpdatedBy,
    // Company Setup fields
    string? LogoUrl = null,
    string? Industry = null,
    string? CompanySize = null,
    string? Website = null,
    // Contact fields
    string? ContactEmail = null,
    string? ContactMobile = null,
    // Address fields
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? StateProvince = null,
    string? PostalCode = null,
    string? Country = null,
    // Setup tracking
    bool IsSetupComplete = false,
    int SetupStep = 0
);
