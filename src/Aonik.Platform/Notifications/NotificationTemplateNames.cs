namespace Aonik.Platform.Notifications;

/// <summary>
/// Well-known notification template names used throughout the platform.
/// These names are used as keys when resolving templates via the
/// <see cref="Services.Notifications.NotificationTemplateService"/>.
/// </summary>
public static class NotificationTemplateNames
{
    public const string WelcomeEmail = "registration.welcome-email";
    public const string EmailConfirmation = "registration.email-confirmation";
    public const string EmailOtp = "registration.email-otp";
    public const string SmsOtp = "registration.sms-otp";
}
