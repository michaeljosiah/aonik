namespace Aonik.Finance.Contracts.Services.Partners.Connectors;

public interface IPartnerConnectorResolver
{
    IPartnerPayoutConnector ResolvePayoutConnector(string providerCode);
    IPartnerCollectionConnector ResolveCollectionConnector(string providerCode);
    IPartnerBillPaymentConnector ResolveBillPaymentConnector(string providerCode);
    IPartnerWebhookTranslator ResolveWebhookTranslator(string providerCode);

    bool TryResolvePayoutConnector(
        PartnerConnectorQuery query, out IPartnerPayoutConnector? connector);

    bool TryResolvePreferredPayoutConnector(
        PartnerConnectorQuery query, out IPartnerPayoutConnector? connector);

    bool TryResolveCollectionConnector(
        PartnerConnectorQuery query, out IPartnerCollectionConnector? connector);
    bool TryResolveBillPaymentConnector(
        PartnerConnectorQuery query, out IPartnerBillPaymentConnector? connector);
}

public sealed record PartnerConnectorQuery(
    PartnerServiceCategory Category, string? Country, string? Currency, string? Method);
