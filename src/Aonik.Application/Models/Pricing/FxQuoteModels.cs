namespace Aonik.Application.Models.Pricing;

public record FxQuoteListResponse(
    Guid Id,
    string BaseCurrency,
    string TargetCurrency,
    decimal Rate,
    DateTime ExpiresAt,
    string? Provider,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record FxQuoteDetailResponse(
    Guid Id,
    Guid TenantId,
    string BaseCurrency,
    string TargetCurrency,
    decimal Rate,
    DateTime ExpiresAt,
    string? Provider,
    string MetadataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateFxQuoteRequest(
    string BaseCurrency,
    string TargetCurrency,
    decimal Rate,
    DateTime ExpiresAt,
    string? Provider,
    string? MetadataJson);

public record UpdateFxQuoteRequest(
    decimal Rate,
    DateTime ExpiresAt,
    string? Provider,
    string? MetadataJson);
