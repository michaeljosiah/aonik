using Aonik.Application.Models.Pricing;

namespace Aonik.Application.Services.Pricing;

public interface IPricingService
{
    Task<PricingQuoteResponse> GetBillPaymentQuoteAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken = default);
}
