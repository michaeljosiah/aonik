using Aonik.Finance.Services.Partners.Connectors;

namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

internal interface IFlutterwaveBillsConfigProvider
{
    /// <summary>Legacy-default / global v3 options from the <c>Finance.Partners.Flutterwave.Bills.*</c> settings.</summary>
    Task<FlutterwaveBillsOptions> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves v3 options for a specific connector row (Spec 042 §8): bundle secret key + environment-derived
    /// base URL when bound, else the fail-closed legacy fallback.
    /// </summary>
    Task<FlutterwaveBillsOptions> GetAsync(ConnectorBinding binding, CancellationToken cancellationToken = default);
}
