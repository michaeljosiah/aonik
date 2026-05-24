using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Infrastructure.Communication.Configuration;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using EmailMessage = Aonik.Platform.Contracts.Services.Messaging.EmailMessage;


namespace Aonik.Infrastructure.Communication;

public class AzureCommunicationEmailSender : IEmailSender
{
    private const string DefaultActiveProvider = "AzureCommunicationServices";
    private const string LegacyAzureConnectionString = "Communication.Azure.ConnectionString";
    private const string LegacyAzureEmailFromAddress = "Communication.Azure.Email.FromAddress";

    private readonly ISettingProvider _settingProvider;
    private readonly CommunicationOptions _options;
    private readonly ILogger<AzureCommunicationEmailSender> _logger;

    public AzureCommunicationEmailSender(
        ISettingProvider settingProvider,
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationEmailSender> logger)
    {
        _settingProvider = settingProvider;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Azure.ConnectionString);

    public string ProviderName => "AzureCommunicationServices";

    public string? UnconfiguredReason
        => IsConfigured ? null : "Communication.Email.AzureCommunicationServices.ConnectionString is missing.";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // Hard fail (with a typed, caller-recoverable exception) instead
        // of silently swallowing the message. Previously this method
        // returned without throwing when the client was null — making
        // every invite look "sent" while no email actually went out.
        var settings = await ResolveSettingsAsync(cancellationToken);
        if (!string.Equals(settings.ActiveProvider, ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new MessagingNotConfiguredException(
                channel: "Email",
                reason: $"Email provider '{settings.ActiveProvider}' is selected, but this deployment only has {ProviderName} registered.");
        }

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new MessagingNotConfiguredException(
                channel: "Email",
                reason: "Communication.Email.AzureCommunicationServices.ConnectionString is missing.");
        }

        EmailClient client;
        try
        {
            client = new EmailClient(settings.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Communication email client could not be initialized. Email sending will be unavailable.");
            throw new MessagingNotConfiguredException(
                channel: "Email",
                reason: $"Azure Communication email client failed to initialise: {ex.Message}");
        }

        var fromAddress = string.IsNullOrWhiteSpace(message.From)
            ? settings.FromAddress
            : message.From;

        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("Communication.Email.AzureCommunicationServices.FromAddress is required for email sending.");

        var recipients = new EmailRecipients(
            new List<EmailAddress> { new(message.To) });

        var content = new EmailContent(message.Subject);

        if (message.IsHtml)
        {
            content.Html = message.Body;
        }
        else
        {
            content.PlainText = message.Body;
        }

        var emailMessage = new Azure.Communication.Email.EmailMessage(fromAddress, recipients, content);

        await client.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken);

        _logger.LogInformation("Sent email to {Recipient}", message.To);
    }

    private async Task<AzureEmailRuntimeSettings> ResolveSettingsAsync(CancellationToken cancellationToken)
    {
        var activeProvider = await _settingProvider.GetAsync(CommunicationSettingNames.EmailProvider, cancellationToken)
                             ?? DefaultActiveProvider;
        var connectionString = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.EmailAzureConnectionString,
            LegacyAzureConnectionString);
        var fromAddress = await GetFirstConfiguredValueAsync(
            cancellationToken,
            CommunicationSettingNames.EmailAzureFromAddress,
            LegacyAzureEmailFromAddress);

        return new AzureEmailRuntimeSettings(
            activeProvider,
            string.IsNullOrWhiteSpace(connectionString) ? _options.Azure.ConnectionString : connectionString,
            string.IsNullOrWhiteSpace(fromAddress) ? _options.Azure.Email.FromAddress : fromAddress);
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

    private sealed record AzureEmailRuntimeSettings(
        string ActiveProvider,
        string? ConnectionString,
        string? FromAddress);
}
