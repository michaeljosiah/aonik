using Aonik.PersonalFinance.Contracts.Services;

namespace Aonik.PersonalFinance.Services.Accounts.Linking;

/// <summary>
/// Resolves an <see cref="IPersonalAccountLinkProviderGateway"/> by provider
/// code (case-insensitive). The set of registered gateways is supplied via DI.
/// </summary>
internal sealed class AccountLinkProviderResolver
{
    private readonly IEnumerable<IPersonalAccountLinkProviderGateway> _providerGateways;

    public AccountLinkProviderResolver(IEnumerable<IPersonalAccountLinkProviderGateway> providerGateways)
    {
        _providerGateways = providerGateways;
    }

    public IPersonalAccountLinkProviderGateway Resolve(string provider)
    {
        var normalized = provider.Trim();

        var gateway = _providerGateways.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (gateway == null)
        {
            throw new ArgumentException($"Unsupported account-link provider '{provider}'.", nameof(provider));
        }

        return gateway;
    }
}
