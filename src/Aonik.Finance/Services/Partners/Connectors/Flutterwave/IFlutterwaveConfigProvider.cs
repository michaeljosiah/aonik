namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

internal interface IFlutterwaveConfigProvider
{
    Task<FlutterwaveOptions> GetAsync(CancellationToken cancellationToken = default);
}
