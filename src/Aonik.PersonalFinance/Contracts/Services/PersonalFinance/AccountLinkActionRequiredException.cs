namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public sealed class AccountLinkActionRequiredException : InvalidOperationException
{
    public AccountLinkActionRequiredException(
        Guid connectionId,
        string provider,
        string requiredAction,
        string message,
        string? providerErrorCode = null)
        : base(message)
    {
        ConnectionId = connectionId;
        Provider = provider;
        RequiredAction = requiredAction;
        ProviderErrorCode = providerErrorCode;
    }

    public Guid ConnectionId { get; }

    public string Provider { get; }

    public string RequiredAction { get; }

    public string? ProviderErrorCode { get; }

    public bool RequiresReconnect => string.Equals(RequiredAction, "reconnect", StringComparison.OrdinalIgnoreCase);
}
