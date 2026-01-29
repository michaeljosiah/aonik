namespace Aonik.Application.Models.Identity;

public record UpdateTenantRequest(
    string? Name = null,
    string? DefaultCurrency = null,
    string[]? SupportedCountries = null,
    string[]? SupportedCurrencies = null,
    string? Environment = null
);
