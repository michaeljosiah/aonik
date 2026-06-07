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

    /// <summary>
    /// Registers a reusable payout recipient with the partner (e.g. Flutterwave
    /// <c>POST /transfers/recipients</c>) and returns its provider beneficiary id (<c>rcp_…</c>)
    /// for storage on <c>ExternalPayoutAccount.ProviderBeneficiaryId</c>. The request carries the
    /// RAW rail details transiently (via <see cref="RecipientRegistrationRequest.Destination"/>);
    /// they are never persisted. Spec 037 §7.4.1 (G19/G20).
    /// </summary>
    Task<RecipientRegistrationResult> RegisterRecipientAsync(
        RecipientRegistrationRequest request, CancellationToken cancellationToken = default);
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

// Fee is nullable: some providers (e.g. Flutterwave v4) cannot quote a transfer fee pre-send —
// the rate endpoint returns the FX rate and converted amount only; the realized fee comes back on
// the transfer response. A null Fee means "fee known only at execution" (Spec 037 §5.7, G8).
public sealed record PayoutQuoteResult(
    Money? Fee, decimal? FxRate, Money? ConvertedAmount, RawProviderResponse Raw);

public sealed record AccountResolutionRequest(
    string BankCode, string AccountNumber, string? Currency);

public sealed record AccountResolutionResult(
    bool Resolved, string? AccountName, RawProviderResponse Raw);

// Carries the RAW rail details (transient, never persisted) needed to create a provider recipient,
// plus the resolved account name and audit context. Reuses PayoutDestination as the raw carrier.
public sealed record RecipientRegistrationRequest(
    PayoutDestination Destination,
    string Currency,
    string AccountName,
    string? Country,
    IReadOnlyDictionary<string, string>? Metadata);

// ProviderBeneficiaryId is the reusable recipient token (rcp_…) to persist on ExternalPayoutAccount.
public sealed record RecipientRegistrationResult(
    bool Registered, string? ProviderBeneficiaryId, string? AccountName, RawProviderResponse Raw);
