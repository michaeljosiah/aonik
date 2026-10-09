using Aonik.SharedKernel.Events.Integration;

namespace Aonik.SharedKernel.Abstractions.Loyalty;

/// <summary>Tenant-scoped loyalty; callers resolve customer ownership, never accept a public party selector.</summary>
public interface ILoyaltyService
{
    Task<LoyaltyPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<LoyaltyBalance> GetBalanceAsync(Guid partyId, CancellationToken cancellationToken = default);
    Task<LoyaltyActivityPage> GetActivityAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<LoyaltyBalance> MarkSeenAsync(Guid partyId, long mark, CancellationToken cancellationToken = default);
    Task<LoyaltyOperationRef> AdjustAsync(LoyaltyAdjustment command, CancellationToken cancellationToken = default);
    Task ReverseRefundAsync(LoyaltyRefund command, CancellationToken cancellationToken = default);
    Task AttachVerifiedGuestAsync(AccountAccessVerifiedEvent verified, CancellationToken cancellationToken = default);
}
