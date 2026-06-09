namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Carries the per-call resolved <see cref="FlutterwaveBillsOptions"/> down the v3 HttpClient pipeline via
/// <c>HttpRequestMessage.Options</c> (Spec 042 §7), so <see cref="FlutterwaveBillsAuthHandler"/> stamps the
/// bound connector's secret key with no ambient state.
/// </summary>
internal static class FlutterwaveBillsRequestContext
{
    public static readonly HttpRequestOptionsKey<FlutterwaveBillsOptions> OptionsKey =
        new("aonik.flutterwave.bills.options");
}
