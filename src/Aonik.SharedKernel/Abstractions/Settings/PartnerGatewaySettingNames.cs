namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Well-known settings for partner gateway credentials/configuration. Kept in SharedKernel because
/// Platform registers/writes the keys and Finance reads them at runtime.
/// </summary>
public static class PartnerGatewaySettingNames
{
    public const string FlutterwaveEnabled = "Finance.Partners.Flutterwave.Enabled";
    public const string FlutterwaveBaseUrl = "Finance.Partners.Flutterwave.BaseUrl";
    public const string FlutterwaveIdpTokenUrl = "Finance.Partners.Flutterwave.IdpTokenUrl";
    public const string FlutterwaveClientId = "Finance.Partners.Flutterwave.ClientId";
    public const string FlutterwaveClientSecret = "Finance.Partners.Flutterwave.ClientSecret";
    public const string FlutterwaveEncryptionKey = "Finance.Partners.Flutterwave.EncryptionKey";
    public const string FlutterwaveDefaultTransferPurpose = "Finance.Partners.Flutterwave.DefaultTransferPurpose";
    public const string FlutterwaveSigningSecret = "Finance.Partners.Webhooks.Flutterwave.SigningSecret";

    // Flutterwave v3 Bills connector (Spec 040). Distinct transport from the v4 keys above: a static
    // secret key (FLWSECK-…), supplied via environment / user-secrets, never committed.
    public const string FlutterwaveBillsEnabled = "Finance.Partners.Flutterwave.Bills.Enabled";
    public const string FlutterwaveBillsBaseUrl = "Finance.Partners.Flutterwave.Bills.BaseUrl";
    public const string FlutterwaveBillsSecretKey = "Finance.Partners.Flutterwave.Bills.SecretKey";
}
