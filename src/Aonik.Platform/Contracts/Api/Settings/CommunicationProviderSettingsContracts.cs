namespace Aonik.Platform.Contracts.Api.Settings;

// ── Snapshot ────────────────────────────────────────────────────────

public record CommunicationProviderSettingsResponse(
    EmailChannelSettingsResponse Email,
    SmsChannelSettingsResponse Sms);

public record EmailChannelSettingsResponse(
    string ActiveProvider,
    AzureEmailSettingsResponse? AzureCommunicationServices);

public record SmsChannelSettingsResponse(
    string ActiveProvider,
    AzureSmsSettingsResponse? AzureCommunicationServices);

public record AzureEmailSettingsResponse(
    bool HasConnectionString,
    string? FromAddress);

public record AzureSmsSettingsResponse(
    bool HasConnectionString,
    string? FromPhoneNumber);

// ── Update ──────────────────────────────────────────────────────────

public record CommunicationProviderSettingsUpdateRequest(
    EmailChannelSettingsUpdateRequest? Email,
    SmsChannelSettingsUpdateRequest? Sms);

public record EmailChannelSettingsUpdateRequest(
    string ActiveProvider,
    AzureEmailSettingsUpdateRequest? AzureCommunicationServices);

public record SmsChannelSettingsUpdateRequest(
    string ActiveProvider,
    AzureSmsSettingsUpdateRequest? AzureCommunicationServices);

public record AzureEmailSettingsUpdateRequest(
    string? ConnectionString,
    string? FromAddress);

public record AzureSmsSettingsUpdateRequest(
    string? ConnectionString,
    string? FromPhoneNumber);

// ── Test send (unchanged) ───────────────────────────────────────────

/// <summary>
/// Posted to <c>POST /admin/settings/communication-provider/test-send</c>.
/// Fires a one-off email/SMS to the supplied recipient using the
/// currently-active provider for that channel.
/// </summary>
public record SendCommunicationTestRequest(
    string Channel,        // "Email" or "SMS"
    string Recipient,      // email address or E.164 phone
    string? Subject,       // ignored for SMS
    string? Body);

public record SendCommunicationTestResponse(
    bool Sent,
    string Channel,
    string Provider,
    string? ErrorMessage);
