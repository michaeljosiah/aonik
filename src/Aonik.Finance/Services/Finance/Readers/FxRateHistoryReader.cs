using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.SharedKernel.Abstractions.Finance;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// Finance-side implementation of <see cref="IFxRateHistoryReader"/>. Delegates to the module's
/// <see cref="IFxRateService"/> and projects its <c>FxRateHistoryResult</c> onto the SharedKernel
/// shape, so PersonalFinance's "Simi" FX tool can read a rate series without depending on
/// <c>Aonik.Finance.Contracts.Services.Pricing</c>.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class FxRateHistoryReader : IFxRateHistoryReader
{
    private readonly IFxRateService _fxRateService;

    public FxRateHistoryReader(IFxRateService fxRateService)
    {
        _fxRateService = fxRateService;
    }

    public async Task<FxRateHistory> GetRateHistoryAsync(
        string baseCurrency,
        string targetCurrency,
        int days,
        CancellationToken cancellationToken = default)
    {
        var result = await _fxRateService.GetRateHistoryAsync(
            baseCurrency, targetCurrency, days, cancellationToken);

        return new FxRateHistory(
            result.BaseCurrency,
            result.TargetCurrency,
            result.Rates.Select(point => new FxRateHistoryPoint(point.Date, point.Rate)).ToList(),
            result.Signal,
            result.SignalReason);
    }
}
