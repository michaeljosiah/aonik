namespace Aonik.Finance.Contracts.Services.Partners.Connectors;

public interface IPartnerWebhookTranslator
{
    string ProviderCode { get; }
    bool VerifySignature(PartnerWebhookEnvelope envelope, string signingSecret);
    PartnerWebhookEvent Translate(PartnerWebhookEnvelope envelope);
}

public sealed record PartnerWebhookEnvelope(
    string ProviderCode, IReadOnlyDictionary<string, string> Headers, string Body);

public sealed record PartnerWebhookEvent(
    PartnerServiceCategory Category, string EventType,
    PartnerReference Reference, PartnerTransactionStatus Status, RawProviderResponse Raw);
