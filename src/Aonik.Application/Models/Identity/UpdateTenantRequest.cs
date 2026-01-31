namespace Aonik.Application.Models.Identity;

public record UpdateTenantRequest(
    string? Name = null,
    string? DefaultCurrency = null,
    string[]? SupportedCountries = null,
    string[]? SupportedCurrencies = null,
    string? Environment = null,
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
    bool? IsSetupComplete = null,
    int? SetupStep = null
);
