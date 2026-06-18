namespace Aonik.Commerce.Services.Promotions;

/// <summary>
/// Discount/coupon management for the Commerce module (Spec 042 §5 follow-up). Returns DTOs.
/// </summary>
public interface IDiscountService
{
    Task<DiscountDto> CreateAsync(CreateDiscountCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a coupon code and computes the discount amount for a subtotal, validating active
    /// state, expiry, currency, and redemption limit. Returns a zero result for a null/blank code.
    /// </summary>
    Task<DiscountComputation> ComputeAsync(string? code, decimal subtotal, string currency, CancellationToken cancellationToken = default);

    /// <summary>Records a redemption (increments the usage counter). No-op for a null discount id.</summary>
    Task MarkRedeemedAsync(Guid? discountId, CancellationToken cancellationToken = default);
}

public record CreateDiscountCommand(
    string Code,
    string Kind,
    decimal Value,
    string? Currency = null,
    int? MaxRedemptions = null,
    DateTime? ExpiresAt = null);

public record DiscountDto(Guid Id, string Code, string Kind, decimal Value, string? Currency, bool IsActive, int? MaxRedemptions, int TimesRedeemed, DateTime? ExpiresAt);

/// <summary>The outcome of applying a coupon: the matched discount (if any) and the amount to deduct.</summary>
public record DiscountComputation(Guid? DiscountId, string? Code, decimal Amount);
