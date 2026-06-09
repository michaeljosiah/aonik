namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Carries the per-call resolved <see cref="FlutterwaveOptions"/> down the HttpClient pipeline via
/// <c>HttpRequestMessage.Options</c> (Spec 042 §7). This is how a connector bound to a specific
/// <c>Connector</c> row threads its credentials to <see cref="FlutterwaveAuthHandler"/> /
/// <see cref="FlutterwaveTokenProvider"/> without ambient state — each request carries its own options, so
/// concurrent payouts on two accounts never alias.
/// </summary>
internal static class FlutterwaveRequestContext
{
    public static readonly HttpRequestOptionsKey<FlutterwaveOptions> OptionsKey = new("aonik.flutterwave.options");
}
