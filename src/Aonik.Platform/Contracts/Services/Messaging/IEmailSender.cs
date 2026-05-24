namespace Aonik.Platform.Contracts.Services.Messaging;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap, synchronous probe: is the underlying email provider
    /// configured and ready to dispatch? Used by the Admin UI to warn
    /// the operator before they trigger flows that depend on a
    /// working email channel (e.g. user invitations, password reset
    /// emails). When this returns <c>false</c>, callers can decide
    /// whether to proceed (some flows still create a record locally —
    /// the invite placeholder + token survive a dead mail channel and
    /// can be re-sent once delivery is fixed) or hard-block.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Short, operator-readable name of the active provider (e.g.
    /// "AzureCommunicationServices", "SendGrid", "None"). Surfaced via
    /// the messaging-health endpoint so the UI can name the missing
    /// thing in any warning it shows.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// When <see cref="IsConfigured"/> is <c>false</c>, returns a short
    /// operator-readable reason (e.g. "Communication:Azure:ConnectionString
    /// is missing"). Returns <c>null</c> when configured.
    /// </summary>
    string? UnconfiguredReason { get; }
}
