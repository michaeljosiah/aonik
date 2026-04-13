using System.Collections.Concurrent;
using Aonik.Platform.Services.Settings;

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

            // ── Communication ─────────────────────────────────────────────
            [CommunicationSettingNames.AzureEmailFromAddress] = new SettingDefinition(CommunicationSettingNames.AzureEmailFromAddress),
            [CommunicationSettingNames.AzureSmsFromPhoneNumber] = new SettingDefinition(CommunicationSettingNames.AzureSmsFromPhoneNumber),

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
            [AiSettingNames.UserMemoryBackend] = new SettingDefinition(AiSettingNames.UserMemoryBackend, "SqlServer"),

            // ── Text To Speech ───────────────────────────────────────────
            [TextToSpeechSettingNames.TenantProfile] = new SettingDefinition(TextToSpeechSettingNames.TenantProfile),
        });

    public static SettingDefinition? Get(string key)
    {
        return Definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<SettingDefinition> All => Definitions.Values.ToList();
}
