namespace Aonik.Platform.Contracts.Models.Messaging;

/// <summary>
/// Response from <c>GET /admin/messaging/health</c>. Surfaces whether
/// the platform has a working email + SMS provider so the Admin UI
/// can warn the operator before they trigger flows that depend on
/// outbound communication (e.g. user invitations, password reset
/// emails, phone verification OTPs).
/// </summary>
public sealed record MessagingHealthResponse(
    MessagingChannelHealth Email,
    MessagingChannelHealth Sms);

/// <summary>
/// Per-channel health snapshot.
/// </summary>
/// <param name="Configured">
/// <c>true</c> if the provider has the configuration it needs to
/// actually dispatch messages (e.g. an ACS connection string). When
/// <c>false</c>, calls to the channel will throw
/// <see cref="Services.Messaging.MessagingNotConfiguredException"/>.
/// </param>
/// <param name="Provider">
/// Short provider identifier, e.g. <c>AzureCommunicationServices</c>.
/// </param>
/// <param name="Reason">
/// Operator-readable explanation of why the channel is unconfigured.
/// <c>null</c> when <paramref name="Configured"/> is <c>true</c>.
/// </param>
public sealed record MessagingChannelHealth(
    bool Configured,
    string Provider,
    string? Reason);
