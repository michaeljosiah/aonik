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
    private readonly string _unconfiguredReason;

    public AzureCommunicationSmsSender(
        IOptions<CommunicationOptions> options,
        ILogger<AzureCommunicationSmsSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        _unconfiguredReason = string.Empty;

        if (string.IsNullOrWhiteSpace(_options.Azure.ConnectionString))
        {
            _unconfiguredReason = "Communication:Azure:ConnectionString is missing in app settings.";
            _logger.LogWarning("Azure Communication connection string not configured for SMS sending.");
            return;
        }

        try
        {
            _client = new SmsClient(_options.Azure.ConnectionString);
        }
        catch (Exception ex)
        {
            _unconfiguredReason = $"Azure Communication SMS client failed to initialise: {ex.Message}";
            _logger.LogWarning(ex, "Azure Communication SMS client could not be initialized. SMS sending will be unavailable.");
        }
    }

    public bool IsConfigured => _client != null;

    public string ProviderName => "AzureCommunicationServices";

    public string? UnconfiguredReason
        => _client != null ? null : (string.IsNullOrEmpty(_unconfiguredReason)
            ? "Azure Communication Services is not configured."
            : _unconfiguredReason);

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        // See AzureCommunicationEmailSender — same rationale: throw a
        // typed exception so callers can distinguish "not configured"
        // from "delivery failed" and report both honestly.
        if (_client == null)
        {
            throw new MessagingNotConfiguredException(
                channel: "SMS",
                reason: string.IsNullOrEmpty(_unconfiguredReason)
                    ? "Azure Communication Services is not configured."
                    : _unconfiguredReason);
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
