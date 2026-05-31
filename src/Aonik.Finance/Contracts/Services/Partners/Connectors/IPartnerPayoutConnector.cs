using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Contracts.Services.Partners.Connectors;

public interface IPartnerPayoutConnector : IPartnerConnector
{
    Task<PayoutInitiationResult> InitiatePayoutAsync(
        PayoutInstruction instruction, CancellationToken cancellationToken = default);

    Task<PayoutStatusResult> GetPayoutStatusAsync(
        PartnerReference reference, CancellationToken cancellationToken = default);

    Task<PayoutQuoteResult> QuotePayoutAsync(
        PayoutQuoteRequest request, CancellationToken cancellationToken = default);

    Task<AccountResolutionResult> ResolveAccountAsync(
        AccountResolutionRequest request, CancellationToken cancellationToken = default);
}

public abstract record PayoutDestination;

public sealed record BankAccountDestination(
    string BankCode, string AccountNumber, string? BranchCode, string? AccountName)
    : PayoutDestination;

public sealed record MobileMoneyDestination(
    string Network, string PhoneNumber, string? AccountName) : PayoutDestination;

public sealed record WalletDestination(string WalletId, string? AccountName) : PayoutDestination;

public sealed record PayoutInstruction(
    string ClientReference,
    Money Amount,
    string DebitCurrency,
    PayoutDestination Destination,
    string Narration,
    string? CallbackUrl,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record PayoutInitiationResult(
    PartnerReference Reference,
    PartnerTransactionStatus Status,
    Money? Fee,
    RawProviderResponse Raw);

public sealed record PayoutStatusResult(
    PartnerReference Reference,
    PartnerTransactionStatus Status,
    Money? Fee,
    RawProviderResponse Raw);

public sealed record PayoutQuoteRequest(
    Money Amount, string DestinationCurrency, PayoutDestination? Destination);

public sealed record PayoutQuoteResult(
    Money Fee, decimal? FxRate, Money? ConvertedAmount, RawProviderResponse Raw);

public sealed record AccountResolutionRequest(
    string BankCode, string AccountNumber, string? Currency);

public sealed record AccountResolutionResult(
    bool Resolved, string? AccountName, RawProviderResponse Raw);
