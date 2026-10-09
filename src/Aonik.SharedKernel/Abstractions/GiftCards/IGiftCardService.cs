namespace Aonik.SharedKernel.Abstractions.GiftCards;

/// <summary>Tenant-scoped bearer value. Calling boundaries authorize the cart or original purchase source.</summary>
public interface IGiftCardService
{
    Task<GiftCardPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<GiftCardBalance?> GetBalanceAsync(string code, CancellationToken cancellationToken = default);
    Task<GiftCardAuthorization> AuthorizeForCartAsync(string code, Guid cartId, CancellationToken cancellationToken = default);
    Task<GiftCardFundingQuote> QuoteAsync(GiftCardQuoteRequest request, CancellationToken cancellationToken = default);
    Task<GiftCardIssuedInfo?> GetIssuedAsync(GiftCardPurchaseSource source, CancellationToken cancellationToken = default);
    Task<GiftCardFulfilmentSecret> GetFulfilmentSecretAsync(GiftCardPurchaseSource source, CancellationToken cancellationToken = default);
}
