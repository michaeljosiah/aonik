using Aonik.Finance.Contracts.Models.Pricing;

namespace Aonik.Finance.Contracts.Services.Pricing;

public interface IPricingService
{
    Task<PricingQuoteResponse> GetBillPaymentQuoteAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken = default);
}
