using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Settings;

/// <summary>
/// Resolves communication settings from the platform settings store
/// for the Admin UI's <c>SettingsCommunicationPage</c>. Read-only —
/// updates throw with a clear "configure via env vars" message so the
/// UI can render the operator-facing explanation.
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
        var activeProvider = await _settingProvider.GetAsync(CommunicationSettingNames.Provider, cancellationToken)
                             ?? DefaultActiveProvider;

        var connectionString = await _settingProvider.GetAsync(CommunicationSettingNames.AzureConnectionString, cancellationToken);
        var emailFromAddress = await _settingProvider.GetAsync(CommunicationSettingNames.AzureEmailFromAddress, cancellationToken);
        var smsFromPhoneNumber = await _settingProvider.GetAsync(CommunicationSettingNames.AzureSmsFromPhoneNumber, cancellationToken);

        return new CommunicationProviderSettingsSnapshot(
            activeProvider,
            new AzureCommunicationSettingsSnapshot(
                HasConnectionString: !string.IsNullOrWhiteSpace(connectionString),
                EmailFromAddress: emailFromAddress,
                SmsFromPhoneNumber: smsFromPhoneNumber));
    }

    public Task<CommunicationProviderSettingsSnapshot> UpdateAsync(
        CommunicationProviderSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        // Matches AuthProviderSettingsService — the Admin UI shows the
        // current values but writes happen via appsettings / env vars.
        // The exception message is surfaced verbatim in the UI's error
        // banner, so it has to read well for operators.
        throw new InvalidOperationException(
            "Communication provider settings are configuration-managed. "
            + "Update Communication:Azure:ConnectionString (and related keys) "
            + "in appsettings/environment variables and restart the API.");
    }
}
