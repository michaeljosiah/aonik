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

    /// <summary>
    /// Sent by <c>/admin/users/invite</c> and <c>/admin/users/{id}/resend-invite</c>.
    /// Carries the tenant-scoped sign-in URL with the one-shot invite
    /// token + expiry + assigned roles.
    /// </summary>
    public const string AdminUserInvitation = "admin.user-invitation";
}
