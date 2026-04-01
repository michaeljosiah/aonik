using Azure.Communication.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Infrastructure.Communication.Configuration;

namespace Aonik.Infrastructure.Communication;

public class AzureCommunicationSmsSender : ISmsSender
{
    private readonly SmsClient? _client;
    private readonly CommunicationOptions _options;
    private readonly ILogger<AzureCommunicationSmsSender> _logger;

    public AzureCommunicationSmsSender(
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationSmsSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Azure.ConnectionString))
        {
            _logger.LogWarning("Azure Communication connection string not configured for SMS sending.");
            return;
        }

        try
        {
            _client = new SmsClient(_options.Azure.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Communication SMS client could not be initialized. SMS sending will be unavailable.");
        }
    }

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            _logger.LogWarning(
                "SMS not sent (Azure Communication Services not configured). To: {To}, Body: {Body}",
                message.To,
                message.Body);
            return;
        }

        var fromNumber = string.IsNullOrWhiteSpace(message.From)
            ? _options.Azure.Sms.FromPhoneNumber
            : message.From;

        if (string.IsNullOrWhiteSpace(fromNumber))
            throw new InvalidOperationException("Communication:Azure:Sms:FromPhoneNumber is required for SMS sending.");

        await _client.SendAsync(
            from: fromNumber,
            to: message.To,
            message: message.Body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Sent SMS to {Recipient}", message.To);
    }
}
