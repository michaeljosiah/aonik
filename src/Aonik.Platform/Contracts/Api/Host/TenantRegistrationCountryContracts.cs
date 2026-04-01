namespace Aonik.Platform.Contracts.Api.Host;

public record TenantRegistrationCountriesResponse(
    Guid TenantId,
    string Name,
    string[] AllowedOriginCountries
);
