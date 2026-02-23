namespace Aonik.Platform.Contracts.Models.Identity;

public record CreateTenantRequest(
    string Name,
    string Environment,
    string DefaultCurrency,
    string[] SupportedCountries,
    string[]? SupportedCurrencies = null
);
