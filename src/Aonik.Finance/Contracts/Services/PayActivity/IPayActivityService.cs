using Aonik.Finance.Contracts.Api.PayActivity;

namespace Aonik.Finance.Contracts.Services.PayActivity;

/// <summary>
/// Service for retrieving pay activity (orders/payments) for the current
/// authenticated user, powering the mobile Pay dashboard.
/// </summary>
public interface IPayActivityService
{
    /// <summary>
    /// Returns the recent pay activity summary for the current user.
    /// </summary>
    Task<PayActivitySummaryResponse> GetRecentActivityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full detail for a single transaction by ID.
    /// </summary>
    Task<PayActivityTransactionDetailResponse?> GetTransactionDetailAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);
}
