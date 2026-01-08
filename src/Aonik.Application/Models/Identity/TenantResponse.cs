namespace Aonik.Application.Models.Identity;

public record TenantResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Environment,
    string DefaultCurrency,
    string[] SupportedCountries,
    string Status,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime? UpdatedAt,
    Guid? UpdatedBy
);
