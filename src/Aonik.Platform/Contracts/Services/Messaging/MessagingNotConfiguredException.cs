namespace Aonik.Platform.Contracts.Services.Messaging;

/// <summary>
/// Thrown by <see cref="IEmailSender"/> / <see cref="ISmsSender"/>
/// implementations when an operator tries to dispatch a message
/// without the underlying provider being configured (e.g. no Azure
/// Communication Services connection string in app settings).
///
/// Callers should treat this as a soft failure: the operation that
/// requested the message (e.g. user invite) usually still completes —
/// the placeholder + token are created — but the recipient won't see
/// anything until an operator configures email/SMS and triggers a
/// re-send. Use <c>IEmailSender.IsConfigured</c> to detect this state
/// up-front and warn the user before they take an irreversible action.
/// </summary>
public sealed class MessagingNotConfiguredException : InvalidOperationException
{
    public MessagingNotConfiguredException(string channel, string reason)
        : base($"{channel} is not configured: {reason}")
    {
        Channel = channel;
        Reason = reason;
    }

    /// <summary>Channel name, e.g. "Email" or "SMS".</summary>
    public string Channel { get; }

    /// <summary>Short, operator-readable reason. Safe to surface in UI.</summary>
    public string Reason { get; }
}
