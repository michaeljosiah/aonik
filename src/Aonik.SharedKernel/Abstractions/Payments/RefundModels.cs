namespace Aonik.SharedKernel.Abstractions.Payments;

public sealed record RefundSelection(string ComponentId, decimal? Amount = null, bool FullRemaining = false);
public sealed record RefundDraft(string Reason, IReadOnlyList<RefundSelection> Selections);
public sealed record RefundRequest(Guid RefundId, string Reason, IReadOnlyList<RefundSelection> Selections,
    string ExpectedPreviewVersion);
public sealed record RefundComponentDto(string ComponentId, string Kind, string Label, decimal OriginalAmount,
    decimal RemainingAmount, bool CanRefund, string? UnavailableReason = null);
public sealed record RefundPreviewDto(string Currency, string Version, IReadOnlyList<RefundSelection> Selections,
    decimal Total, decimal CashAmount, decimal GiftAmount, long RedeemedPointsToRestore,
    long EarnedPointsToReverse, IReadOnlyList<string> Warnings);
public sealed record RefundDto(Guid RefundId, Guid OrderId, string Status, DateTime RequestedAtUtc,
    Guid? RequestedBy, string Reason, decimal Total, string Currency, decimal CashAmount, decimal GiftAmount,
    long RedeemedPointsToRestore, long EarnedPointsToReverse, DateTime? EffectsAppliedAtUtc,
    string? FailureReason, bool CanReconcile);
public sealed record RefundContextDto(string Currency, bool CanRequest, string? DisabledReason,
    decimal RemainingTotal, string Status, IReadOnlyList<RefundComponentDto> Components,
    IReadOnlyList<RefundDto> History);

/// <summary>Staff operations; each implementation enforces tenant and payment permissions.</summary>
public interface IOrderRefundService
{
    Task<RefundContextDto> GetAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<RefundDto?> GetRefundAsync(Guid orderId, Guid refundId, CancellationToken cancellationToken = default);
    Task<RefundPreviewDto> PreviewAsync(Guid orderId, RefundDraft draft, CancellationToken cancellationToken = default);
    Task<RefundDto> RequestAsync(Guid orderId, RefundRequest request, CancellationToken cancellationToken = default);
    Task<RefundDto> ReconcileAsync(Guid orderId, Guid refundId, CancellationToken cancellationToken = default);
}

/// <summary>Original paid facts read by Finance from Commerce. Never an HTTP input.</summary>
public sealed record CheckoutRefundSource(Guid OrderId, Guid PaymentIntentId, Guid? InvoiceId, string Currency,
    decimal Total, decimal GiftAmount, decimal CardAmount, IReadOnlyList<CheckoutRefundComponent> Components);
public sealed record CheckoutRefundComponent(string ComponentId, Guid? OrderItemId, string Kind, string Label,
    decimal Amount, decimal GiftFundedValue, long EarnedPoints, long RedeemedPoints);
public interface ICheckoutRefundSourceReader
{
    Task<CheckoutRefundSource?> ReadAsync(Guid orderId, CancellationToken cancellationToken = default);
}
