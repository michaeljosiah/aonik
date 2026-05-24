namespace Aonik.Platform.Contracts.Api.Settings;

public record CommunicationProviderSettingsResponse(
    string ActiveProvider,
    AzureCommunicationSettingsResponse Azure);

public record AzureCommunicationSettingsResponse(
    bool HasConnectionString,
    string? EmailFromAddress,
    string? SmsFromPhoneNumber);

public record CommunicationProviderSettingsUpdateRequest(
    string ActiveProvider,
    AzureCommunicationSettingsUpdateRequest? Azure);

public record AzureCommunicationSettingsUpdateRequest(
    string? ConnectionString,
    string? EmailFromAddress,
    string? SmsFromPhoneNumber);

/// <summary>
/// Posted to <c>POST /admin/settings/communication-provider/test-send</c>.
/// Fires a one-off email/SMS to the supplied recipient using the
/// currently-active provider so operators can verify their configuration
/// without doing a full invite cycle.
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
