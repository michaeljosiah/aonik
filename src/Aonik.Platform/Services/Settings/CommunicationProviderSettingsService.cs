using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Settings;

/// <summary>
/// Resolves communication settings from the platform settings store
/// for the Admin UI's <c>SettingsCommunicationPage</c>. Read-only —
/// updates throw a "configure via env vars" message that the UI
/// surfaces in its error banner.
///
/// Email and SMS are treated as independent channels: each has its
/// own active provider and its own credentials. ACS happens to bundle
/// the two today (one connection string per resource) but the schema
/// makes no such assumption — operators can mix providers freely
/// (e.g. SendGrid email + Twilio SMS in a future build).
/// </summary>
internal sealed class CommunicationProviderSettingsService : ICommunicationProviderSettingsService
{
    private const string DefaultActiveProvider = "AzureCommunicationServices";

    private readonly ISettingProvider _settingProvider;

    public CommunicationProviderSettingsService(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public async Task<CommunicationProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        // Email channel
        var emailProvider = await _settingProvider.GetAsync(CommunicationSettingNames.EmailProvider, cancellationToken)
                            ?? DefaultActiveProvider;
        var emailAzureConnString = await _settingProvider.GetAsync(CommunicationSettingNames.EmailAzureConnectionString, cancellationToken);
        var emailAzureFromAddress = await _settingProvider.GetAsync(CommunicationSettingNames.EmailAzureFromAddress, cancellationToken);

        // SMS channel
        var smsProvider = await _settingProvider.GetAsync(CommunicationSettingNames.SmsProvider, cancellationToken)
                          ?? DefaultActiveProvider;
        var smsAzureConnString = await _settingProvider.GetAsync(CommunicationSettingNames.SmsAzureConnectionString, cancellationToken);
        var smsAzureFromPhone = await _settingProvider.GetAsync(CommunicationSettingNames.SmsAzureFromPhoneNumber, cancellationToken);

        return new CommunicationProviderSettingsSnapshot(
            Email: new EmailChannelSettingsSnapshot(
                ActiveProvider: emailProvider,
                AzureCommunicationServices: new AzureEmailSettingsSnapshot(
                    HasConnectionString: !string.IsNullOrWhiteSpace(emailAzureConnString),
                    FromAddress: emailAzureFromAddress)),
            Sms: new SmsChannelSettingsSnapshot(
                ActiveProvider: smsProvider,
                AzureCommunicationServices: new AzureSmsSettingsSnapshot(
                    HasConnectionString: !string.IsNullOrWhiteSpace(smsAzureConnString),
                    FromPhoneNumber: smsAzureFromPhone)));
    }

    public Task<CommunicationProviderSettingsSnapshot> UpdateAsync(
        CommunicationProviderSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        // Mirrors AuthProviderSettingsService — the UI shows the
        // current values but writes happen via appsettings / env vars.
        // The exception message is surfaced verbatim in the UI's error
        // banner, so it has to read well for operators.
        throw new InvalidOperationException(
            "Communication provider settings are configuration-managed. "
            + "Update the Communication:Email:* and Communication:Sms:* keys "
            + "in appsettings/environment variables and restart the API.");
    }
}
