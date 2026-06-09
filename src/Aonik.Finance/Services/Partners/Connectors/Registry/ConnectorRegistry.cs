using Aonik.Finance.Contracts.Services.Partners.Connectors;

namespace Aonik.Finance.Services.Partners.Connectors.Registry;

/// <summary>
/// Static, code-owned registry of every connector <em>kind</em> the platform ships (Spec 042 §4). It is
/// the single source of truth for "what credential fields does this kind need" (drives the credential
/// form + bundle validation) and "what non-secret config is allowed" (drives <c>ConfigJson</c> validation,
/// §10). New connector code adds a kind here; nothing routes by provider code alone.
/// </summary>
internal static class ConnectorRegistry
{
    // ── Connector kind codes (stored in Connector.ConnectorType) ───────────────────────────────
    public const string FlutterwavePayoutV4 = "flutterwave-payout-v4";
    public const string FlutterwaveBillsV3 = "flutterwave-bills-v3";

    // ── Provider codes (IPartnerConnector.ProviderCode) ────────────────────────────────────────
    public const string ProviderFlutterwave = "Flutterwave";

    // ── Environment names (the ConfigJson.environment value) ───────────────────────────────────
    public const string EnvironmentSandbox = "sandbox";
    public const string EnvironmentProduction = "production";

    // ── Credential field names (keys in the bundle's protected secret map) ─────────────────────
    public const string FieldClientId = "clientId";
    public const string FieldClientSecret = "clientSecret";
    public const string FieldEncryptionKey = "encryptionKey";
    public const string FieldSigningSecret = "signingSecret";
    public const string FieldSecretKey = "secretKey";

    // ── Config field names (keys in ConfigJson) ────────────────────────────────────────────────
    public const string ConfigEnvironment = "environment";
    public const string ConfigDefaultTransferPurpose = "defaultTransferPurpose";
    public const string ConfigCountry = "country";

    // Flutterwave OAuth IdP token endpoint — the same realm for sandbox AND production
    // (Spec 037 §5.2 table: "OAuth IdP … (both)").
    private const string FlutterwaveIdpTokenUrl =
        "https://idp.flutterwave.com/realms/flutterwave/protocol/openid-connect/token";

    private static readonly IReadOnlyList<ConnectorKindDescriptor> AllKinds = new[]
    {
        new ConnectorKindDescriptor(
            Kind: FlutterwavePayoutV4,
            ProviderCode: ProviderFlutterwave,
            Port: PartnerServiceCategory.Payout,
            DisplayName: "Flutterwave payout (v4 OAuth)",
            CredentialFields: new[]
            {
                new ConnectorCredentialField(FieldClientId, "Client ID", Required: true),
                new ConnectorCredentialField(FieldClientSecret, "Client secret", Required: true),
                // Card-collection AES key (Spec 037 phase 2) and the webhook signing secret are both
                // optional for a payout-only connector.
                new ConnectorCredentialField(FieldEncryptionKey, "Encryption key", Required: false),
                new ConnectorCredentialField(FieldSigningSecret, "Webhook signing secret", Required: false),
            },
            ConfigFields: new[]
            {
                new ConnectorConfigField(
                    ConfigEnvironment, "Environment", Required: true,
                    AllowedValues: new[] { EnvironmentSandbox, EnvironmentProduction },
                    DefaultValue: EnvironmentSandbox),
                new ConnectorConfigField(
                    ConfigDefaultTransferPurpose, "Default transfer purpose", Required: false,
                    AllowedValues: null, DefaultValue: "family_maintenance"),
            },
            Environments: new[]
            {
                new ConnectorEnvironment(
                    EnvironmentSandbox,
                    "https://developersandbox-api.flutterwave.com",
                    FlutterwaveIdpTokenUrl),
                // NOTE: the v4 production base URL is UNCONFIRMED per Spec 037 §O6 ("Confirm
                // f4bexperience.flutterwave.com vs another host … verify against the live dashboard
                // before go-live"). It lives here as a code constant so changing it is a reviewed
                // one-line edit, not a free-text money-path field. Verify before the production cutover.
                new ConnectorEnvironment(
                    EnvironmentProduction,
                    "https://f4bexperience.flutterwave.com",
                    FlutterwaveIdpTokenUrl),
            }),

        new ConnectorKindDescriptor(
            Kind: FlutterwaveBillsV3,
            ProviderCode: ProviderFlutterwave,
            Port: PartnerServiceCategory.BillPayment,
            DisplayName: "Flutterwave bills (v3 secret key)",
            CredentialFields: new[]
            {
                new ConnectorCredentialField(FieldSecretKey, "Secret key (FLWSECK-…)", Required: true),
            },
            ConfigFields: new[]
            {
                new ConnectorConfigField(
                    ConfigEnvironment, "Environment", Required: true,
                    AllowedValues: new[] { EnvironmentSandbox, EnvironmentProduction },
                    DefaultValue: EnvironmentSandbox),
                // Flutterwave Bills is NG-only today (Spec 040 §3); kept as config for future markets.
                new ConnectorConfigField(
                    ConfigCountry, "Country", Required: false, AllowedValues: null, DefaultValue: "NG"),
            },
            // v3 has no separate sandbox host — the same base URL is used for both and the secret key
            // (test vs live FLWSECK-) selects the environment (Spec 040 §3).
            Environments: new[]
            {
                new ConnectorEnvironment(EnvironmentSandbox, "https://api.flutterwave.com/v3", IdpTokenUrl: null),
                new ConnectorEnvironment(EnvironmentProduction, "https://api.flutterwave.com/v3", IdpTokenUrl: null),
            }),
    };

    private static readonly IReadOnlyDictionary<string, ConnectorKindDescriptor> ByKind =
        AllKinds.ToDictionary(k => k.Kind, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<ConnectorKindDescriptor> All => AllKinds;

    public static ConnectorKindDescriptor? Get(string? kind) =>
        kind is not null && ByKind.TryGetValue(kind.Trim(), out var descriptor) ? descriptor : null;

    public static bool TryGet(string? kind, out ConnectorKindDescriptor? descriptor)
    {
        descriptor = Get(kind);
        return descriptor is not null;
    }

    public static ConnectorKindDescriptor GetRequired(string kind) =>
        Get(kind) ?? throw new InvalidOperationException($"Connector kind '{kind}' is not registered.");

    public static IEnumerable<ConnectorKindDescriptor> ForProvider(string providerCode) =>
        AllKinds.Where(k => string.Equals(k.ProviderCode, providerCode?.Trim(), StringComparison.OrdinalIgnoreCase));
}
