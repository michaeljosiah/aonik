using Aonik.Finance.Contracts.Models.Pricing;

namespace Aonik.Finance.Contracts.Services.Pricing;

public interface IPricingService
{
    Task<PricingQuoteResponse> GetBillPaymentQuoteAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quote a remittance corridor. Reuses the bill-payment pricing calculation but persists the
    /// quote with <c>QuoteType = "Remittance"</c> so storage does not conflate the two intents.
    /// Spec 036 §6.3.
    /// </summary>
    Task<PricingQuoteResponse> GetRemittanceQuoteAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken = default);
}
