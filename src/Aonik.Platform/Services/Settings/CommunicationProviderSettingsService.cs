using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Settings;

/// <summary>
/// Resolves communication settings from the platform settings store
/// for the Admin UI's <c>SettingsCommunicationPage</c>.
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
    private const string LegacyAzureConnectionString = "Communication.Azure.ConnectionString";
    private const string LegacyAzureEmailFromAddress = "Communication.Azure.Email.FromAddress";
    private const string LegacyAzureSmsFromPhoneNumber = "Communication.Azure.Sms.FromPhoneNumber";

    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;

    public CommunicationProviderSettingsService(
        ISettingProvider settingProvider,
        ISettingManager settingManager)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
    }

    public async Task<CommunicationProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        // Email channel
        var emailProvider = await _settingProvider.GetAsync(CommunicationSettingNames.EmailProvider, cancellationToken)
                            ?? DefaultActiveProvider;
        var emailAzureConnString = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.EmailAzureConnectionString,
            LegacyAzureConnectionString);
        var emailAzureFromAddress = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.EmailAzureFromAddress,
            LegacyAzureEmailFromAddress);

        // SMS channel
        var smsProvider = await _settingProvider.GetAsync(CommunicationSettingNames.SmsProvider, cancellationToken)
                          ?? DefaultActiveProvider;
        var smsAzureConnString = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.SmsAzureConnectionString,
            LegacyAzureConnectionString);
        var smsAzureFromPhone = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.SmsAzureFromPhoneNumber,
            LegacyAzureSmsFromPhoneNumber);

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
        return UpdateInternalAsync(update, cancellationToken);
    }

    private async Task<CommunicationProviderSettingsSnapshot> UpdateInternalAsync(
        CommunicationProviderSettingsUpdate update,
        CancellationToken cancellationToken)
    {
        if (update.Email != null)
        {
            await _settingManager.SetAsync(
                CommunicationSettingNames.EmailProvider,
                update.Email.ActiveProvider,
                cancellationToken);

            if (update.Email.AzureCommunicationServices != null)
            {
                var azure = update.Email.AzureCommunicationServices;
                if (!string.IsNullOrWhiteSpace(azure.ConnectionString))
                {
                    await _settingManager.SetAsync(
                        CommunicationSettingNames.EmailAzureConnectionString,
                        azure.ConnectionString,
                        cancellationToken);
                }

                await _settingManager.SetAsync(
                    CommunicationSettingNames.EmailAzureFromAddress,
                    azure.FromAddress,
                    cancellationToken);
            }
        }

        if (update.Sms != null)
        {
            await _settingManager.SetAsync(
                CommunicationSettingNames.SmsProvider,
                update.Sms.ActiveProvider,
                cancellationToken);

            if (update.Sms.AzureCommunicationServices != null)
            {
                var azure = update.Sms.AzureCommunicationServices;
                if (!string.IsNullOrWhiteSpace(azure.ConnectionString))
                {
                    await _settingManager.SetAsync(
                        CommunicationSettingNames.SmsAzureConnectionString,
                        azure.ConnectionString,
                        cancellationToken);
                }

                await _settingManager.SetAsync(
                    CommunicationSettingNames.SmsAzureFromPhoneNumber,
                    azure.FromPhoneNumber,
                    cancellationToken);
            }
        }

        return await GetAsync(cancellationToken);
    }

    private async Task<string?> GetFirstConfiguredValueAsync(
        CancellationToken cancellationToken,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = await _settingProvider.GetAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
