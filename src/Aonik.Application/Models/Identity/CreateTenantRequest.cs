namespace Aonik.Application.Models.Identity;

public record CreateTenantRequest(
    string Name,
    string Environment,
    string DefaultCurrency,
    string[] SupportedCountries,
    string[]? SupportedCurrencies = null
);
