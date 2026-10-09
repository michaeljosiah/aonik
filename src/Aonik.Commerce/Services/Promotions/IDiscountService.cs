using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Promotions;

/// <summary>
/// Discount/coupon management for the Commerce module (Spec 042 §5 follow-up). Returns DTOs.
/// </summary>
public interface IDiscountService
{
    Task<DiscountDto> CreateAsync(CreateDiscountCommand command, CancellationToken cancellationToken = default);
    Task<PagedResult<DiscountDto>> ListAsync(ListDiscountsQuery query, CancellationToken cancellationToken = default);
    Task<DiscountDto> UpdateAsync(Guid discountId, UpdateDiscountCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a coupon code and computes the discount amount for a subtotal, validating active
    /// state, expiry, currency, and redemption limit. Returns a zero result for a null/blank code.
    /// </summary>
    Task<DiscountComputation> ComputeAsync(string? code, IReadOnlyList<DiscountChargeLine> lines, string currency, CancellationToken cancellationToken = default);

    // Checkout owns the transaction and SaveChanges for these tracked operations.
    Task<Guid?> ReserveTrackedAsync(Guid cartId, Guid attemptId, string? code, IReadOnlyList<DiscountChargeLine> lines,
        string currency, DiscountComputation expected, CancellationToken cancellationToken = default);
    Task CommitTrackedAsync(Guid cartId, Guid reservationId, Guid attemptId, Guid discountId, CancellationToken cancellationToken = default);
    Task ReleaseTrackedAsync(Guid cartId, Guid reservationId, Guid attemptId, CancellationToken cancellationToken = default);
    void Detach(Guid cartId, Guid? discountId = null);

    /// <summary>Records a redemption (increments the usage counter). No-op for a null discount id.</summary>
    Task MarkRedeemedAsync(Guid? discountId, CancellationToken cancellationToken = default);
}

public record CreateDiscountCommand(
    string Code,
    string Kind,
    decimal Value,
    string? Currency = null,
    int? MaxRedemptions = null,
    DateTime? ExpiresAt = null,
    IReadOnlyList<Guid>? EligibleProductIds = null);

public record ListDiscountsQuery(int Page = 1, int PageSize = 20, string? Search = null, bool? IsActive = null);
public record UpdateDiscountCommand(string Kind, decimal Value, bool IsActive, string? Currency,
    int? MaxRedemptions, DateTime? ExpiresAt, IReadOnlyList<Guid>? EligibleProductIds, string ExpectedVersion);
public record DiscountDto(Guid Id, string Code, string Kind, decimal Value, string? Currency, bool IsActive,
    int? MaxRedemptions, int TimesRedeemed, DateTime? ExpiresAt, IReadOnlyList<Guid>? EligibleProductIds = null,
    int ReservedCount = 0, string Version = "");

public record DiscountChargeLine(int Index, Guid? ProductId, decimal Amount, string Kind = "Goods");
public record DiscountAllocation(int ItemIndex, decimal Amount);
public record OrderDiscountAllocation(Guid OrderItemId, decimal Amount);

/// <summary>The outcome of applying a coupon: the matched discount (if any) and the amount to deduct.</summary>
public record DiscountComputation(Guid? DiscountId, string? Code, decimal Amount, IReadOnlyList<DiscountAllocation> Allocations);
