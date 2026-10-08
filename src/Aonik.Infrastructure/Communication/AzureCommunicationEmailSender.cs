using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Infrastructure.Communication.Configuration;
using Aonik.Infrastructure.Settings;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using EmailMessage = Aonik.Platform.Contracts.Services.Messaging.EmailMessage;

namespace Aonik.Infrastructure.Communication;

public class AzureCommunicationEmailSender : IEmailSender
{
    private const string DefaultActiveProvider = "AzureCommunicationServices";
    private const string LegacyAzureConnectionString = "Communication.Azure.ConnectionString";
    private const string LegacyAzureEmailFromAddress = "Communication.Azure.Email.FromAddress";

    private readonly ISettingProvider _settingProvider;
    private readonly TenantFirstSettingReader _settings;
    private readonly CommunicationOptions _options;
    private readonly ILogger<AzureCommunicationEmailSender> _logger;

    public AzureCommunicationEmailSender(
        ISettingProvider settingProvider,
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationEmailSender> logger,
        ITenantSettingStore tenantSettings,
        ITenantProvider tenantProvider)
    {
        _settingProvider = settingProvider;
        _settings = new TenantFirstSettingReader(tenantSettings, settingProvider, tenantProvider);
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Legacy options-only probe; SendAsync resolves current tenant/stored settings independently.</summary>
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
            client = CreateClient(settings.ConnectionString);
        }
        catch (Exception)
        {
            _logger.LogWarning("Azure Communication email client could not be initialized. Check the configured connection string.");
            throw new MessagingNotConfiguredException(
                channel: "Email",
                reason: "Azure Communication email client could not be initialized. Check the configured connection string.");
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

        var operation = await client.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken);

        // Provider completion means acceptance for delivery, not arrival in the recipient's mailbox.
        _logger.LogInformation("Email provider operation {OperationId} completed with status {Status}.",
            operation.Id, operation.Value.Status);
    }

    protected virtual EmailClient CreateClient(string connectionString) => new(connectionString);

    private async Task<AzureEmailRuntimeSettings> ResolveSettingsAsync(CancellationToken cancellationToken)
    {
        var activeProvider = await _settings.ReadAsync(CommunicationSettingNames.EmailProvider, cancellationToken)
                             ?? DefaultActiveProvider;
        var connectionString = await _settings.ReadAsync(CommunicationSettingNames.EmailAzureConnectionString, cancellationToken)
                               ?? await _settingProvider.GetAsync(LegacyAzureConnectionString, cancellationToken);
        var fromAddress = await _settings.ReadAsync(CommunicationSettingNames.EmailAzureFromAddress, cancellationToken)
                          ?? await _settingProvider.GetAsync(LegacyAzureEmailFromAddress, cancellationToken);

        return new AzureEmailRuntimeSettings(
            activeProvider,
            string.IsNullOrWhiteSpace(connectionString) ? _options.Azure.ConnectionString : connectionString,
            string.IsNullOrWhiteSpace(fromAddress) ? _options.Azure.Email.FromAddress : fromAddress);
    }

    private sealed record AzureEmailRuntimeSettings(
        string ActiveProvider,
        string? ConnectionString,
        string? FromAddress);
}
