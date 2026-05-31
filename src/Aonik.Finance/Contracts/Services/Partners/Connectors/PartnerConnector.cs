namespace Aonik.Finance.Contracts.Services.Partners.Connectors;

// Airtime / data top-up is a distinct service category but rides the bill-payment port (section 5.5).
public enum PartnerServiceCategory { Payout, Collection, BillPayment, AirtimeTopup }

// Single normalized vocabulary every vendor status maps onto.
public enum PartnerTransactionStatus
{
    Pending, Processing, RequiresAction,
    Succeeded, Failed, Reversed, Expired, Unknown
}

// Our idempotent client ref (tx_ref / clientRef) + the provider's ref (flw_ref / paymentRef).
public sealed record PartnerReference(string ClientReference, string? ProviderReference);

// Vendor-native code / message / raw payload, preserved for audit, mapping, and requery.
public sealed record RawProviderResponse(string? Code, string? Message, string? PayloadJson);

public sealed record PartnerConnectorCapability(
    PartnerServiceCategory Category,
    IReadOnlyCollection<string> Countries,
    IReadOnlyCollection<string> Currencies,
    IReadOnlyCollection<string> Methods);

public interface IPartnerConnector
{
    string ProviderCode { get; }
    IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; }
}
