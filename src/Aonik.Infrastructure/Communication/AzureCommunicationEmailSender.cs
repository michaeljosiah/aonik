using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Infrastructure.Communication.Configuration;
using EmailMessage = Aonik.Platform.Contracts.Services.Messaging.EmailMessage;


namespace Aonik.Infrastructure.Communication;

public class AzureCommunicationEmailSender : IEmailSender
{
    private readonly EmailClient? _client;
    private readonly CommunicationOptions _options;
    private readonly ILogger<AzureCommunicationEmailSender> _logger;
    private readonly string _unconfiguredReason;

    public AzureCommunicationEmailSender(
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        _unconfiguredReason = string.Empty;

        if (string.IsNullOrWhiteSpace(_options.Azure.ConnectionString))
        {
            _unconfiguredReason = "Communication:Azure:ConnectionString is missing in app settings.";
            _logger.LogWarning("Azure Communication connection string not configured for email sending.");
            return;
        }

        try
        {
            _client = new EmailClient(_options.Azure.ConnectionString);
        }
        catch (Exception ex)
        {
            _unconfiguredReason = $"Azure Communication email client failed to initialise: {ex.Message}";
            _logger.LogWarning(ex, "Azure Communication email client could not be initialized. Email sending will be unavailable.");
        }
    }

    public bool IsConfigured => _client != null;

    public string ProviderName => "AzureCommunicationServices";

    public string? UnconfiguredReason
        => _client != null ? null : (string.IsNullOrEmpty(_unconfiguredReason)
            ? "Azure Communication Services is not configured."
            : _unconfiguredReason);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // Hard fail (with a typed, caller-recoverable exception) instead
        // of silently swallowing the message. Previously this method
        // returned without throwing when the client was null — making
        // every invite look "sent" while no email actually went out.
        if (_client == null)
        {
            throw new MessagingNotConfiguredException(
                channel: "Email",
                reason: string.IsNullOrEmpty(_unconfiguredReason)
                    ? "Azure Communication Services is not configured."
                    : _unconfiguredReason);
        }

        var fromAddress = string.IsNullOrWhiteSpace(message.From)
            ? _options.Azure.Email.FromAddress
            : message.From;

        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("Communication:Azure:Email:FromAddress is required for email sending.");

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

        await _client.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken);

        _logger.LogInformation("Sent email to {Recipient}", message.To);
    }
}
