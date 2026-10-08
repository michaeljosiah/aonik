namespace Aonik.SharedKernel.Abstractions.Messaging;

public static class TransactionalEmailTemplateNames
{
    public const string OrderConfirmation = "commerce.order-confirmation";
    public const string AccountSetupAccess = "identity.account-setup-access";
    public const string PasswordReset = "identity.password-reset";
    public const string EmailChangeConfirmation = "identity.email-change-confirmation";
}
