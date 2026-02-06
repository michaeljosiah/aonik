namespace Aonik.Application.Models.Orders;

public record ListOrdersRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? OrderType = null,
    string? Search = null
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
