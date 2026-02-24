namespace Aonik.Finance.Contracts.Models.Catalog;

public record CatalogServiceFieldValidationRequest(Dictionary<string, string> FieldValues);
public record CatalogServiceFieldValidationResult(bool IsValid, DateTimeOffset ValidatedAt, string? ErrorCode, string? ErrorMessage, string? AccountHolderName, Dictionary<string, string>? AdditionalInfo);
