namespace Aonik.Finance.Contracts.Models.Pricing;

public record FeePolicyConditions(
    string? ServiceCode,
    string? OriginCountry,
    string? DestinationCountry,
    string? OriginCurrency,
    string? DestinationCurrency,
    string? CustomerTier,
    decimal? MinTransferAmount,
    decimal? MaxTransferAmount,
    decimal? MinFee,
    decimal? MaxFee,
    int? MarkupBps,
    string? RateProvider,
    string? RoundingMode,
    IReadOnlyCollection<FeeBreakdownDefinition>? FeeBreakdown);

public record FeeBreakdownDefinition(
    string Code,
    string Description,
    string CalculationType);
