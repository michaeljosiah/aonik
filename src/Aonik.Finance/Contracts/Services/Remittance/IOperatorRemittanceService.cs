using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Payments;

namespace Aonik.Finance.Contracts.Services.Remittance;

/// <summary>Tenant-scoped staff operations; separate from customer ownership-authorised endpoints.</summary>
public interface IOperatorRemittanceService
{
    Task<RemittanceQuoteResponse> QuoteForCustomerAsync(RemittanceQuoteRequest request, CancellationToken cancellationToken = default);
    Task<RemittanceOrderResponse> ConfirmForCustomerAsync(ConfirmRemittanceRequest request, string idempotencyKey, bool confirmExecution, CancellationToken cancellationToken = default);
    Task<RemittanceOrderResponse?> GetForOperatorAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayoutBeneficiaryResponse>> ListDestinationsForCustomerAsync(Guid customerPartyId, CancellationToken cancellationToken = default);
}
