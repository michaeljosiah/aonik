namespace Aonik.Platform.Contracts.Services.Messaging;

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap, synchronous probe: is the underlying SMS provider
    /// configured and ready to dispatch? Mirrors <see cref="IEmailSender.IsConfigured"/>.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Short, operator-readable provider name.</summary>
    string ProviderName { get; }

    /// <summary>Mirrors <see cref="IEmailSender.UnconfiguredReason"/>.</summary>
    string? UnconfiguredReason { get; }
}
