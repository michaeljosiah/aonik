namespace Aonik.Platform.Services.Settings;

/// <summary>
/// Canonical setting keys for the outbound messaging stack. Mirrors the
/// pattern established by <see cref="AuthSettingNames"/> — keys live in
/// the platform settings store (<c>ISettingProvider</c>), source of
/// truth for both runtime resolution and the
/// <c>SettingsCommunicationPage</c> in the Admin UI.
///
/// Today only Azure Communication Services is wired up; SendGrid /
/// Mailgun / etc. would slot in as additional <c>Provider</c> values
/// with parallel namespaces.
/// </summary>
public static class CommunicationSettingNames
{
    /// <summary>Active provider identifier. Today: "AzureCommunicationServices".</summary>
    public const string Provider = "Communication.Provider";

    public const string AzureConnectionString = "Communication.Azure.ConnectionString";
    public const string AzureEmailFromAddress = "Communication.Azure.Email.FromAddress";
    public const string AzureSmsFromPhoneNumber = "Communication.Azure.Sms.FromPhoneNumber";
}
