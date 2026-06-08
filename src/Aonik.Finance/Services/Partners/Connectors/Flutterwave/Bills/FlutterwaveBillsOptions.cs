namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave.Bills;

/// <summary>
/// Configuration for the Flutterwave <strong>v3</strong> Bills connector (Spec 040). Bound from
/// <c>Finance:Partners:Flutterwave:Bills</c>. The v3 Bills API authenticates with a <em>static secret
/// key</em> (<c>Authorization: Bearer FLWSECK-…</c>) — deliberately NOT the v4 OAuth credentials the
/// payout connector uses (Spec 040 §3), so this is its own options type with its own base URL and key.
/// The secret is supplied via environment / user-secrets, never committed to appsettings.
/// </summary>
internal sealed class FlutterwaveBillsOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.flutterwave.com/v3";

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Flutterwave Bills is NG-only (Spec 040 §3); the connector hard-defaults to this.</summary>
    public string Country { get; set; } = "NG";

    public bool IsConfigured()
        => Enabled
           && !string.IsNullOrWhiteSpace(BaseUrl)
           && !string.IsNullOrWhiteSpace(SecretKey);
}
