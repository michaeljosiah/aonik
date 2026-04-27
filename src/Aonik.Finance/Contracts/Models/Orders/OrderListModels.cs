namespace Aonik.Finance.Contracts.Models.Orders;

public record ListOrdersRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? OrderType = null,
    string? Search = null,
    Guid? PayerPartyId = null,
    /// <summary>Inclusive lower bound on Order.CreatedAt (UTC).</summary>
    DateTime? CreatedFromUtc = null,
    /// <summary>Exclusive upper bound on Order.CreatedAt (UTC).</summary>
    DateTime? CreatedToUtc = null
);

public record OrderListItem(
    Guid OrderId,
    string OrderType,
    string Status,
    Guid? PayerPartyId,
    string PayerName,
    string? OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal? TotalAmountOut,
    string? DestinationCurrency,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
