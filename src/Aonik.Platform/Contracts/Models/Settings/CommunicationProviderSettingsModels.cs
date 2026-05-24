namespace Aonik.Platform.Contracts.Models.Settings;

/// <summary>
/// Read snapshot returned by <see cref="Services.Settings.ICommunicationProviderSettingsService.GetAsync"/>.
/// Mirrors <see cref="AuthProviderSettingsSnapshot"/> — secrets never
/// round-trip through the snapshot, only a <c>Has*</c> indicator that
/// one is set. The Admin UI uses this to render the
/// <c>SettingsCommunicationPage</c> with masked credential fields.
/// </summary>
public record CommunicationProviderSettingsSnapshot(
    string ActiveProvider,
    AzureCommunicationSettingsSnapshot Azure);

public record AzureCommunicationSettingsSnapshot(
    bool HasConnectionString,
    string? EmailFromAddress,
    string? SmsFromPhoneNumber);

/// <summary>
/// Update payload posted by the Admin UI. The service is currently a
/// read-only viewer (matches the auth provider pattern — see
/// <see cref="Services.Settings.AuthProviderSettingsService.UpdateAsync"/>)
/// so this record exists for contract symmetry; the service throws
/// when callers attempt a write, directing operators to configure via
/// environment variables.
/// </summary>
public record CommunicationProviderSettingsUpdate(
    string ActiveProvider,
    AzureCommunicationSettingsUpdate? Azure);

public record AzureCommunicationSettingsUpdate(
    string? ConnectionString,
    string? EmailFromAddress,
    string? SmsFromPhoneNumber);
