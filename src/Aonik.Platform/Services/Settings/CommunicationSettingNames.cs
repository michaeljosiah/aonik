namespace Aonik.Platform.Services.Settings;

/// <summary>
/// Canonical setting keys for the outbound messaging stack. Email and
/// SMS are independent channels — they can be served by different
/// providers (e.g. SendGrid email + Twilio SMS) and have to be
/// configured separately. The key layout reflects that:
///
///   Communication.Email.Provider                                      = "AzureCommunicationServices" | "SendGrid" | ...
///   Communication.Email.AzureCommunicationServices.ConnectionString   = ...
///   Communication.Email.AzureCommunicationServices.FromAddress        = noreply@...
///   Communication.Email.SendGrid.ApiKey                               = (future)
///   Communication.Email.SendGrid.FromAddress                          = (future)
///
///   Communication.Sms.Provider                                        = "AzureCommunicationServices" | "Twilio" | ...
///   Communication.Sms.AzureCommunicationServices.ConnectionString     = ...
///   Communication.Sms.AzureCommunicationServices.FromPhoneNumber      = +44...
///   Communication.Sms.Twilio.AccountSid                               = (future)
///   Communication.Sms.Twilio.AuthToken                                = (future)
///   Communication.Sms.Twilio.FromPhoneNumber                          = (future)
///
/// When an operator uses one ACS resource for both channels they paste
/// the same connection string under both. The schema stays
/// architecturally pure (no shared "Communication.Azure.*" namespace
/// that locks the two channels together).
/// </summary>
public static class CommunicationSettingNames
{
    // ── Email channel ──────────────────────────────────────────────
    public const string EmailProvider = "Communication.Email.Provider";

    public const string EmailAzureConnectionString = "Communication.Email.AzureCommunicationServices.ConnectionString";
    public const string EmailAzureFromAddress      = "Communication.Email.AzureCommunicationServices.FromAddress";

    // Reserved for future SendGrid implementation:
    // public const string EmailSendGridApiKey      = "Communication.Email.SendGrid.ApiKey";
    // public const string EmailSendGridFromAddress = "Communication.Email.SendGrid.FromAddress";

    // ── SMS channel ────────────────────────────────────────────────
    public const string SmsProvider = "Communication.Sms.Provider";

    public const string SmsAzureConnectionString  = "Communication.Sms.AzureCommunicationServices.ConnectionString";
    public const string SmsAzureFromPhoneNumber   = "Communication.Sms.AzureCommunicationServices.FromPhoneNumber";

    // Reserved for future Twilio implementation:
    // public const string SmsTwilioAccountSid      = "Communication.Sms.Twilio.AccountSid";
    // public const string SmsTwilioAuthToken       = "Communication.Sms.Twilio.AuthToken";
    // public const string SmsTwilioFromPhoneNumber = "Communication.Sms.Twilio.FromPhoneNumber";
}
