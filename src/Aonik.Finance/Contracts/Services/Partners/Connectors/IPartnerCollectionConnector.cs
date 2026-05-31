using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Contracts.Services.Partners.Connectors;

public interface IPartnerCollectionConnector : IPartnerConnector
{
    Task<CollectionInitiationResult> InitiateCollectionAsync(
        CollectionInstruction instruction, CancellationToken cancellationToken = default);

    Task<CollectionStatusResult> GetCollectionStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default);

    Task<CollectionRefundResult> RefundCollectionAsync(
        CollectionRefundInstruction instruction, CancellationToken cancellationToken = default);
}

public abstract record CollectionMethod;
public sealed record CardCollection(string? RedirectUrl) : CollectionMethod;
public sealed record BankTransferCollection : CollectionMethod;
public sealed record MobileMoneyCollection(string Network, string PhoneNumber) : CollectionMethod;
public sealed record UssdCollection(string BankCode) : CollectionMethod;

public sealed record PayerContact(
    string? Email, string? PhoneNumber, string? FullName, string? IpAddress);

public sealed record CollectionInstruction(
    string ClientReference,
    Money Amount,
    CollectionMethod Method,
    PayerContact Payer,
    IReadOnlyDictionary<string, string>? Metadata);

// Normalized "next action" relayed to the payer (redirect / pin / ussd / callback).
public sealed record PartnerAuthorizationAction(
    string Mode, string? RedirectUrl, string? UssdCode, string? Reference);

public sealed record CollectionInitiationResult(
    PartnerReference Reference,
    PartnerTransactionStatus Status,
    PartnerAuthorizationAction? NextAction,
    RawProviderResponse Raw);

public sealed record CollectionStatusResult(
    PartnerReference Reference,
    PartnerTransactionStatus Status,
    Money? SettledAmount,
    RawProviderResponse Raw);

public sealed record CollectionRefundInstruction(
    PartnerReference OriginalReference, Money Amount, string? Reason);

public sealed record CollectionRefundResult(
    PartnerReference Reference, PartnerTransactionStatus Status, RawProviderResponse Raw);
