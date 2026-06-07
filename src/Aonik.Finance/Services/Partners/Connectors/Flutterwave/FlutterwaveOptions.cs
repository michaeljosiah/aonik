namespace Aonik.Finance.Services.Partners.Connectors.Flutterwave;

/// <summary>
/// Configuration for the real Flutterwave v4 connector. Bound from
/// <c>Finance:Partners:Flutterwave</c>. Secrets are supplied via environment / user-secrets, not
/// committed to appsettings. The webhook signing secret deliberately does NOT live here — the
/// shipped <c>RemittanceOrderService.ProcessWebhookAsync</c> reads it from
/// <c>Finance:Partners:Webhooks:Flutterwave:SigningSecret</c> (Spec 037 §7.1).
/// </summary>
internal sealed class FlutterwaveOptions
{
    public bool UseRealFlutterwaveApi { get; set; }

    public string BaseUrl { get; set; } = "https://developersandbox-api.flutterwave.com";

    public string IdpTokenUrl { get; set; } =
        "https://idp.flutterwave.com/realms/flutterwave/protocol/openid-connect/token";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Base64 AES-256-GCM key — only needed for card collection (phase 2).</summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Default transfer purpose when the caller supplies none (Flutterwave requires it).</summary>
    public string DefaultTransferPurpose { get; set; } = "family_maintenance";

    public bool IsConfigured()
    {
        return UseRealFlutterwaveApi
            && !string.IsNullOrWhiteSpace(BaseUrl)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
