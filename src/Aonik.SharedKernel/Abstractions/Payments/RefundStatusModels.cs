namespace Aonik.SharedKernel.Abstractions.Payments;

public sealed record OrderRefundStatusDto(string Status, decimal CashReturned, decimal GiftRestored, decimal TotalReturned);

/// <summary>Safe return summary for an already authorized order read; no provider or staff details.</summary>
public interface IOrderRefundStatusReader
{
    Task<OrderRefundStatusDto> ReadAsync(Guid orderId, CancellationToken cancellationToken = default);
}
