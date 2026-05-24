using Azure.Communication.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Infrastructure.Communication.Configuration;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Infrastructure.Communication;

public class AzureCommunicationSmsSender : ISmsSender
{
    private const string DefaultActiveProvider = "AzureCommunicationServices";
    private const string LegacyAzureConnectionString = "Communication.Azure.ConnectionString";
    private const string LegacyAzureSmsFromPhoneNumber = "Communication.Azure.Sms.FromPhoneNumber";

    private readonly ISettingProvider _settingProvider;
    private readonly CommunicationOptions _options;
    private readonly ILogger<AzureCommunicationSmsSender> _logger;

    public AzureCommunicationSmsSender(
        ISettingProvider settingProvider,
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationSmsSender> logger)
    {
        _settingProvider = settingProvider;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Azure.ConnectionString);

    public string ProviderName => "AzureCommunicationServices";

    public string? UnconfiguredReason
        => IsConfigured ? null : "Communication.Sms.AzureCommunicationServices.ConnectionString is missing.";

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        // See AzureCommunicationEmailSender — same rationale: throw a
        // typed exception so callers can distinguish "not configured"
        // from "delivery failed" and report both honestly.
        var settings = await ResolveSettingsAsync(cancellationToken);
        if (!string.Equals(settings.ActiveProvider, ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new MessagingNotConfiguredException(
                channel: "SMS",
                reason: $"SMS provider '{settings.ActiveProvider}' is selected, but this deployment only has {ProviderName} registered.");
        }

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new MessagingNotConfiguredException(
                channel: "SMS",
                reason: "Communication.Sms.AzureCommunicationServices.ConnectionString is missing.");
        }

        SmsClient client;
        try
        {
            client = new SmsClient(settings.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Communication SMS client could not be initialized. SMS sending will be unavailable.");
            throw new MessagingNotConfiguredException(
                channel: "SMS",
                reason: $"Azure Communication SMS client failed to initialise: {ex.Message}");
        }

        var fromNumber = string.IsNullOrWhiteSpace(message.From)
            ? settings.FromPhoneNumber
            : message.From;

        if (string.IsNullOrWhiteSpace(fromNumber))
            throw new InvalidOperationException("Communication.Sms.AzureCommunicationServices.FromPhoneNumber is required for SMS sending.");

        await client.SendAsync(
            from: fromNumber,
            to: message.To,
            message: message.Body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Sent SMS to {Recipient}", message.To);
    }

    private async Task<AzureSmsRuntimeSettings> ResolveSettingsAsync(CancellationToken cancellationToken)
    {
        var activeProvider = await _settingProvider.GetAsync(CommunicationSettingNames.SmsProvider, cancellationToken)
                             ?? DefaultActiveProvider;
        var connectionString = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.SmsAzureConnectionString,
            LegacyAzureConnectionString);
        var fromPhoneNumber = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.SmsAzureFromPhoneNumber,
            LegacyAzureSmsFromPhoneNumber);

        return new AzureSmsRuntimeSettings(
            activeProvider,
            string.IsNullOrWhiteSpace(connectionString) ? _options.Azure.ConnectionString : connectionString,
            string.IsNullOrWhiteSpace(fromPhoneNumber) ? _options.Azure.Sms.FromPhoneNumber : fromPhoneNumber);
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

    private sealed record AzureSmsRuntimeSettings(
        string ActiveProvider,
        string? ConnectionString,
        string? FromPhoneNumber);
}
