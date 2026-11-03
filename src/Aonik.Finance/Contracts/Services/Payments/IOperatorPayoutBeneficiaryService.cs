namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>Staff management of customer-owned payout destinations within the current tenant.</summary>
public interface IOperatorPayoutBeneficiaryService
{
    Task<OperatorBeneficiaryListResponse> ListForCustomerAsync(Guid customerPartyId, CancellationToken cancellationToken = default);
    Task<PayoutBeneficiaryResponse> RegisterForCustomerAsync(VerifyPayoutBeneficiaryRequest request, CancellationToken cancellationToken = default);
    Task UpdateNotesAsync(Guid customerPartyId, Guid beneficiaryPartyId, string notes, CancellationToken cancellationToken = default);
    Task RemoveDestinationAsync(Guid customerPartyId, Guid destinationId, CancellationToken cancellationToken = default);
}

public record OperatorBeneficiaryResponse(PayoutBeneficiaryResponse Destination, string? Notes);
public record OperatorBeneficiaryListResponse(Guid CustomerPartyId, IReadOnlyList<OperatorBeneficiaryResponse> Beneficiaries);
