namespace Aonik.Platform.Contracts.Models.Settings;

/// <summary>
/// Read snapshot returned by
/// <see cref="Services.Settings.ICommunicationProviderSettingsService.GetAsync"/>.
/// Email and SMS are independent — each carries its own active
/// provider plus the per-provider configuration blocks. Secrets are
/// reported via <c>Has*</c> flags and never round-trip through the
/// snapshot.
/// </summary>
public record CommunicationProviderSettingsSnapshot(
    EmailChannelSettingsSnapshot Email,
    SmsChannelSettingsSnapshot Sms);

public record EmailChannelSettingsSnapshot(
    string ActiveProvider,
    AzureEmailSettingsSnapshot? AzureCommunicationServices);

public record SmsChannelSettingsSnapshot(
    string ActiveProvider,
    AzureSmsSettingsSnapshot? AzureCommunicationServices);

public record AzureEmailSettingsSnapshot(
    bool HasConnectionString,
    string? FromAddress);

public record AzureSmsSettingsSnapshot(
    bool HasConnectionString,
    string? FromPhoneNumber);

/// <summary>
/// Update payload. The service currently rejects all writes
/// (configuration-managed via env vars) — see
/// <see cref="Services.Settings.CommunicationProviderSettingsService.UpdateAsync"/>.
/// Records exist for contract symmetry with the auth provider
/// pattern.
/// </summary>
public record CommunicationProviderSettingsUpdate(
    EmailChannelSettingsUpdate? Email,
    SmsChannelSettingsUpdate? Sms);

public record EmailChannelSettingsUpdate(
    string ActiveProvider,
    AzureEmailSettingsUpdate? AzureCommunicationServices);

public record SmsChannelSettingsUpdate(
    string ActiveProvider,
    AzureSmsSettingsUpdate? AzureCommunicationServices);

public record AzureEmailSettingsUpdate(
    string? ConnectionString,
    string? FromAddress);

public record AzureSmsSettingsUpdate(
    string? ConnectionString,
    string? FromPhoneNumber);
