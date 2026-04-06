namespace Aonik.Finance.Contracts.Services.Pricing;

public interface IFxRateService
{
    Task<FxRateResult> GetRateAsync(
        string baseCurrency,
        string targetCurrency,
        CancellationToken cancellationToken = default);

    Task<FxRateHistoryResult> GetRateHistoryAsync(
        string baseCurrency,
        string targetCurrency,
        int days = 7,
        CancellationToken cancellationToken = default);
}

public record FxRateResult(
    Guid FxRateId,
    decimal Rate,
    DateTimeOffset RateTimestamp,
    string? Provider);

public record FxRateHistoryResult(
    string BaseCurrency,
    string TargetCurrency,
    IReadOnlyList<FxRatePoint> Rates,
    string Signal,
    string SignalReason);

public record FxRatePoint(string Date, decimal Rate);
