using Aonik.Finance.Contracts.Services.Partners.Connectors;

namespace Aonik.Finance.Services.Partners.Connectors;

/// <summary>
/// Single selection point for partner connectors over the DI-injected port lists - an improvement
/// on the inline ResolveProvider switch in PublicPaymentService. Direct lookups match a
/// ProviderCode (case-insensitive); the TryResolve* overloads pick a connector whose Capabilities
/// satisfy a PartnerConnectorQuery - the seam a routing engine will later drive. A real vendor is
/// added by registering one more connector against the relevant port(s); no resolver change.
/// </summary>
internal sealed class PartnerConnectorResolver : IPartnerConnectorResolver
{
    private readonly IEnumerable<IPartnerPayoutConnector> _payoutConnectors;
    private readonly IEnumerable<IPartnerCollectionConnector> _collectionConnectors;
    private readonly IEnumerable<IPartnerBillPaymentConnector> _billPaymentConnectors;
    private readonly IEnumerable<IPartnerWebhookTranslator> _webhookTranslators;

    public PartnerConnectorResolver(
        IEnumerable<IPartnerPayoutConnector> payoutConnectors,
        IEnumerable<IPartnerCollectionConnector> collectionConnectors,
        IEnumerable<IPartnerBillPaymentConnector> billPaymentConnectors,
        IEnumerable<IPartnerWebhookTranslator> webhookTranslators)
    {
        _payoutConnectors = payoutConnectors;
        _collectionConnectors = collectionConnectors;
        _billPaymentConnectors = billPaymentConnectors;
        _webhookTranslators = webhookTranslators;
    }

    public IPartnerPayoutConnector ResolvePayoutConnector(string providerCode)
        => ResolveByCode(_payoutConnectors, providerCode, "payout");

    public IPartnerCollectionConnector ResolveCollectionConnector(string providerCode)
        => ResolveByCode(_collectionConnectors, providerCode, "collection");

    public IPartnerBillPaymentConnector ResolveBillPaymentConnector(string providerCode)
        => ResolveByCode(_billPaymentConnectors, providerCode, "bill payment");

    public IPartnerWebhookTranslator ResolveWebhookTranslator(string providerCode)
    {
        var normalized = providerCode.Trim();
        var translator = _webhookTranslators.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (translator is null)
        {
            throw new InvalidOperationException(
                $"Partner webhook translator '{providerCode}' is not configured.");
        }

        return translator;
    }

    public bool TryResolvePayoutConnector(
        PartnerConnectorQuery query, out IPartnerPayoutConnector? connector)
    {
        connector = _payoutConnectors.FirstOrDefault(item => Satisfies(item, query));
        return connector is not null;
    }

    public bool TryResolveCollectionConnector(
        PartnerConnectorQuery query, out IPartnerCollectionConnector? connector)
    {
        connector = _collectionConnectors.FirstOrDefault(item => Satisfies(item, query));
        return connector is not null;
    }

    public bool TryResolveBillPaymentConnector(
        PartnerConnectorQuery query, out IPartnerBillPaymentConnector? connector)
    {
        connector = _billPaymentConnectors.FirstOrDefault(item => Satisfies(item, query));
        return connector is not null;
    }

    private static TConnector ResolveByCode<TConnector>(
        IEnumerable<TConnector> connectors, string providerCode, string portName)
        where TConnector : class, IPartnerConnector
    {
        var normalized = providerCode.Trim();
        var connector = connectors.FirstOrDefault(item =>
            string.Equals(item.ProviderCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (connector is null)
        {
            throw new InvalidOperationException(
                $"Partner {portName} connector '{providerCode}' is not configured.");
        }

        return connector;
    }

    private static bool Satisfies(IPartnerConnector connector, PartnerConnectorQuery query)
        => connector.Capabilities.Any(capability =>
            capability.Category == query.Category
            && (query.Country is null
                || capability.Countries.Contains(query.Country, StringComparer.OrdinalIgnoreCase))
            && (query.Currency is null
                || capability.Currencies.Contains(query.Currency, StringComparer.OrdinalIgnoreCase))
            && (query.Method is null
                || capability.Methods.Contains(query.Method, StringComparer.OrdinalIgnoreCase)));
}
