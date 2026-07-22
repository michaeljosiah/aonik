using System.Collections.Concurrent;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

using CommunicationServiceSettingNames = Aonik.Platform.Services.Settings.CommunicationSettingNames;

namespace Aonik.Platform.Settings;

public static class SettingDefinitions
{
    private static readonly IReadOnlyDictionary<string, SettingDefinition> Definitions =
        new ConcurrentDictionary<string, SettingDefinition>(new Dictionary<string, SettingDefinition>
        {
            // ── Auth ──────────────────────────────────────────────────────
            [AuthSettingNames.Provider] = new SettingDefinition(AuthSettingNames.Provider, "AzureAd", IsVisibleToClients: true),

            [AuthSettingNames.Auth0Domain] = new SettingDefinition(AuthSettingNames.Auth0Domain, IsVisibleToClients: true),
            [AuthSettingNames.Auth0Audience] = new SettingDefinition(AuthSettingNames.Auth0Audience, IsVisibleToClients: true),
            [AuthSettingNames.Auth0ClientId] = new SettingDefinition(AuthSettingNames.Auth0ClientId, IsVisibleToClients: true),
            [AuthSettingNames.Auth0ManagementClientId] = new SettingDefinition(AuthSettingNames.Auth0ManagementClientId, IsVisibleToClients: true),
            [AuthSettingNames.Auth0ManagementClientSecret] = new SettingDefinition(AuthSettingNames.Auth0ManagementClientSecret, IsEncrypted: true),
            [AuthSettingNames.Auth0Connection] = new SettingDefinition(AuthSettingNames.Auth0Connection, "Username-Password-Authentication", IsVisibleToClients: true),
            [AuthSettingNames.Auth0ManagementAudience] = new SettingDefinition(AuthSettingNames.Auth0ManagementAudience),

            [AuthSettingNames.AzureAdAuthority] = new SettingDefinition(AuthSettingNames.AzureAdAuthority, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdAudience] = new SettingDefinition(AuthSettingNames.AzureAdAudience, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdClientId] = new SettingDefinition(AuthSettingNames.AzureAdClientId, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdClientSecret] = new SettingDefinition(AuthSettingNames.AzureAdClientSecret, IsEncrypted: true),
            [AuthSettingNames.AzureAdTenantId] = new SettingDefinition(AuthSettingNames.AzureAdTenantId, IsVisibleToClients: true),
            [AuthSettingNames.AzureAdUpnDomain] = new SettingDefinition(AuthSettingNames.AzureAdUpnDomain),

            // Spec 029 — Keycloak. Authority, Audience, ClientId, and Realm are
            // visible to clients (the admin UI needs them to construct the OIDC
            // client). The two secrets stay server-side and are flagged IsEncrypted
            // so the settings store persists them through ISettingValueProtector.
            [AuthSettingNames.KeycloakAuthority] = new SettingDefinition(AuthSettingNames.KeycloakAuthority, IsVisibleToClients: true),
            [AuthSettingNames.KeycloakAudience] = new SettingDefinition(AuthSettingNames.KeycloakAudience, IsVisibleToClients: true),
            [AuthSettingNames.KeycloakClientId] = new SettingDefinition(AuthSettingNames.KeycloakClientId, IsVisibleToClients: true),
            [AuthSettingNames.KeycloakClientSecret] = new SettingDefinition(AuthSettingNames.KeycloakClientSecret, IsEncrypted: true),
            [AuthSettingNames.KeycloakRealm] = new SettingDefinition(AuthSettingNames.KeycloakRealm, IsVisibleToClients: true),
            [AuthSettingNames.KeycloakAdminClientId] = new SettingDefinition(AuthSettingNames.KeycloakAdminClientId, IsVisibleToClients: true),
            [AuthSettingNames.KeycloakAdminClientSecret] = new SettingDefinition(AuthSettingNames.KeycloakAdminClientSecret, IsEncrypted: true),

            // ── Communication ─────────────────────────────────────────────
            [CommunicationServiceSettingNames.EmailProvider] = new SettingDefinition(CommunicationServiceSettingNames.EmailProvider, "AzureCommunicationServices"),
            [CommunicationServiceSettingNames.EmailAzureConnectionString] = new SettingDefinition(CommunicationServiceSettingNames.EmailAzureConnectionString, IsEncrypted: true),
            [CommunicationServiceSettingNames.EmailAzureFromAddress] = new SettingDefinition(CommunicationServiceSettingNames.EmailAzureFromAddress),
            [CommunicationServiceSettingNames.SmsProvider] = new SettingDefinition(CommunicationServiceSettingNames.SmsProvider, "AzureCommunicationServices"),
            [CommunicationServiceSettingNames.SmsAzureConnectionString] = new SettingDefinition(CommunicationServiceSettingNames.SmsAzureConnectionString, IsEncrypted: true),
            [CommunicationServiceSettingNames.SmsAzureFromPhoneNumber] = new SettingDefinition(CommunicationServiceSettingNames.SmsAzureFromPhoneNumber),

            // Legacy ACS keys retained so existing deployments continue to resolve appsettings/env configuration.
            [CommunicationSettingNames.AzureEmailFromAddress] = new SettingDefinition(CommunicationSettingNames.AzureEmailFromAddress),
            [CommunicationSettingNames.AzureSmsFromPhoneNumber] = new SettingDefinition(CommunicationSettingNames.AzureSmsFromPhoneNumber),

            // ── Partner Gateways ───────────────────────────────────────
            [PartnerGatewaySettingNames.FlutterwaveEnabled] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveEnabled, "false", IsVisibleToClients: true),
            [PartnerGatewaySettingNames.FlutterwaveBaseUrl] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveBaseUrl, "https://developersandbox-api.flutterwave.com", IsVisibleToClients: true),
            [PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveIdpTokenUrl, "https://idp.flutterwave.com/realms/flutterwave/protocol/openid-connect/token", IsVisibleToClients: true),
            [PartnerGatewaySettingNames.FlutterwaveClientId] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveClientId, IsVisibleToClients: true),
            [PartnerGatewaySettingNames.FlutterwaveClientSecret] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveClientSecret, IsEncrypted: true),
            [PartnerGatewaySettingNames.FlutterwaveEncryptionKey] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveEncryptionKey, IsEncrypted: true),
            [PartnerGatewaySettingNames.FlutterwaveDefaultTransferPurpose] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveDefaultTransferPurpose, "family_maintenance", IsVisibleToClients: true),
            [PartnerGatewaySettingNames.FlutterwaveSigningSecret] = new SettingDefinition(PartnerGatewaySettingNames.FlutterwaveSigningSecret, IsEncrypted: true),

            // ── Blob Storage ──────────────────────────────────────────────
            [BlobStorageSettingNames.Provider] = new SettingDefinition(BlobStorageSettingNames.Provider, "Local"),
            [BlobStorageSettingNames.AzureAccountName] = new SettingDefinition(BlobStorageSettingNames.AzureAccountName),
            [BlobStorageSettingNames.ProfilePhotosPublicBaseUrl] = new SettingDefinition(BlobStorageSettingNames.ProfilePhotosPublicBaseUrl),
            [BlobStorageSettingNames.ProductImagesPublicBaseUrl] = new SettingDefinition(BlobStorageSettingNames.ProductImagesPublicBaseUrl),
            [BlobStorageSettingNames.DocumentsPublicBaseUrl] = new SettingDefinition(BlobStorageSettingNames.DocumentsPublicBaseUrl),

            // ── Platform Admin ────────────────────────────────────────────
            [PlatformAdminSettingNames.RoleClaimType] = new SettingDefinition(PlatformAdminSettingNames.RoleClaimType, "roles"),
            [PlatformAdminSettingNames.RoleValue] = new SettingDefinition(PlatformAdminSettingNames.RoleValue, "Aonik.PlatformAdmin"),
            [PlatformAdminSettingNames.ScopeClaimType] = new SettingDefinition(PlatformAdminSettingNames.ScopeClaimType, "aonik_platform_admin"),
            [PlatformAdminSettingNames.AdminEmail0] = new SettingDefinition(PlatformAdminSettingNames.AdminEmail0),

            // ── Bootstrap ─────────────────────────────────────────────────
            [BootstrapSettingNames.Enabled] = new SettingDefinition(BootstrapSettingNames.Enabled, "false"),

            // ── Feature Flags ─────────────────────────────────────────────
            [FeatureFlagSettingNames.BillPaymentsInvoicingCreate] = new SettingDefinition(FeatureFlagSettingNames.BillPaymentsInvoicingCreate, "true"),
            [FeatureFlagSettingNames.BillPaymentsInvoicingIssue] = new SettingDefinition(FeatureFlagSettingNames.BillPaymentsInvoicingIssue, "true"),
            [FeatureFlagSettingNames.BillPaymentsInvoicingPayment] = new SettingDefinition(FeatureFlagSettingNames.BillPaymentsInvoicingPayment, "true"),
            [FeatureFlagSettingNames.BillPaymentsInvoicingDiscounts] = new SettingDefinition(FeatureFlagSettingNames.BillPaymentsInvoicingDiscounts, "false"),
            [FeatureFlagSettingNames.BillPaymentsInvoicingAllocations] = new SettingDefinition(FeatureFlagSettingNames.BillPaymentsInvoicingAllocations, "true"),
            [FeatureFlagSettingNames.BillPaymentsCustomerAccountsManagement] = new SettingDefinition(FeatureFlagSettingNames.BillPaymentsCustomerAccountsManagement, "true"),

            // ── Payabo ───────────────────────────────────────────────────
            [PayaboSettingNames.SetupProfile] = new SettingDefinition(PayaboSettingNames.SetupProfile),

            // ── AI ───────────────────────────────────────────────────────
            [AiSettingNames.Provider] = new SettingDefinition(AiSettingNames.Provider, "Stub"),
            [AiSettingNames.OpenAiApiKey] = new SettingDefinition(AiSettingNames.OpenAiApiKey, IsEncrypted: true),
            [AiSettingNames.OpenAiModel] = new SettingDefinition(AiSettingNames.OpenAiModel, "gpt-5-mini"),
            [AiSettingNames.OpenAiImageModel] = new SettingDefinition(AiSettingNames.OpenAiImageModel, "dall-e-3"),
            [AiSettingNames.UserMemoryBackend] = new SettingDefinition(AiSettingNames.UserMemoryBackend, "Qdrant"),
            [AiSettingNames.OpenTelemetryEnableSensitiveData] = new SettingDefinition(AiSettingNames.OpenTelemetryEnableSensitiveData, "false"),

            // ── Observability ────────────────────────────────────────────
            [ObservabilitySettingNames.AppInsightsAppId] = new SettingDefinition(ObservabilitySettingNames.AppInsightsAppId),
            [ObservabilitySettingNames.AppInsightsApiKey] = new SettingDefinition(ObservabilitySettingNames.AppInsightsApiKey, IsEncrypted: true),

            // ── Text To Speech ───────────────────────────────────────────
            [TextToSpeechSettingNames.TenantProfile] = new SettingDefinition(TextToSpeechSettingNames.TenantProfile),

            // ── Commerce storefront (Spec 066/070) ───────────────────────
            [CommerceSettingNames.StorefrontRecommendedChoiceLabel] = new SettingDefinition(
                CommerceSettingNames.StorefrontRecommendedChoiceLabel,
                DefaultValue: "Recommended",
                IsVisibleToClients: true),

            // Spec 070 §9 — the storefront-config document's tunables. Defaults make an
            // unconfigured storefront serve a valid minimal document rather than 404.
            [CommerceSettingNames.StorefrontResultsPageSize] = new SettingDefinition(
                CommerceSettingNames.StorefrontResultsPageSize,
                DefaultValue: "8",
                IsVisibleToClients: true),
            [CommerceSettingNames.StorefrontBackToTopTriggerJson] = new SettingDefinition(
                CommerceSettingNames.StorefrontBackToTopTriggerJson,
                DefaultValue: """{"type":"cardIndex","value":10}""",
                IsVisibleToClients: true),
            [CommerceSettingNames.StorefrontDeliveryListAmount] = new SettingDefinition(
                CommerceSettingNames.StorefrontDeliveryListAmount,
                DefaultValue: "0",
                IsVisibleToClients: true),
            [CommerceSettingNames.StorefrontDeliveryChargedAmount] = new SettingDefinition(
                CommerceSettingNames.StorefrontDeliveryChargedAmount,
                DefaultValue: "0",
                IsVisibleToClients: true),
            [CommerceSettingNames.StorefrontDefaultBoxProductSlug] = new SettingDefinition(
                CommerceSettingNames.StorefrontDefaultBoxProductSlug,
                IsVisibleToClients: true),
        });

    public static SettingDefinition? Get(string key)
    {
        return Definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<SettingDefinition> All => Definitions.Values.ToList();
}
