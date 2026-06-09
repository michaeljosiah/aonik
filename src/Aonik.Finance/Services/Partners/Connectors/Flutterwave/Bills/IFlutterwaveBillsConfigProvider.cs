namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

internal interface IFlutterwaveBillsConfigProvider
{
    Task<FlutterwaveBillsOptions> GetAsync(CancellationToken cancellationToken = default);
}
