using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.Messaging;
using Aonik.Infrastructure.Communication.Configuration;
using EmailMessage = Aonik.Application.Abstractions.Messaging.EmailMessage;


namespace Aonik.Infrastructure.Communication;

public class AzureCommunicationEmailSender : IEmailSender
{
    private readonly EmailClient? _client;
    private readonly CommunicationOptions _options;
    private readonly ILogger<AzureCommunicationEmailSender> _logger;

    public AzureCommunicationEmailSender(
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Azure.ConnectionString))
        {
            _logger.LogWarning("Azure Communication connection string not configured for email sending.");
            return;
        }

        _client = new EmailClient(_options.Azure.ConnectionString);
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Azure Communication email client is not configured.");

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
