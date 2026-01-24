namespace Aonik.Application.Services.Pricing;

public interface IFxRateService
{
    Task<FxRateResult> GetRateAsync(
        string baseCurrency,
        string targetCurrency,
        CancellationToken cancellationToken = default);
}

public record FxRateResult(
    Guid FxRateId,
    decimal Rate,
    DateTimeOffset RateTimestamp,
    string? Provider);
