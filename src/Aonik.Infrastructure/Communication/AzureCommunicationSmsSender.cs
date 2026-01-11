using Azure.Communication.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.Messaging;
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

        _client = new SmsClient(_options.Azure.ConnectionString);
    }

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Azure Communication SMS client is not configured.");

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
