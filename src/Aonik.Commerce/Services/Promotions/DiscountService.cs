using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Promotions;

/// <summary>Discount/coupon management over <see cref="CommerceDbContext"/> (Spec 042 §5 follow-up).</summary>
internal sealed class DiscountService : IDiscountService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public DiscountService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<DiscountDto> CreateAsync(CreateDiscountCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (command.Kind == DiscountKinds.FixedAmount && string.IsNullOrWhiteSpace(command.Currency))
        {
            throw new ArgumentException("A FixedAmount discount requires a currency.");
        }
        if (command.Kind == DiscountKinds.Percentage && (command.Value <= 0 || command.Value > 100))
        {
            throw new ArgumentException("A Percentage discount value must be between 0 and 100.");
        }

        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = command.Code,
            Kind = command.Kind,
            Value = command.Value,
            Currency = command.Currency,
            IsActive = true,
            MaxRedemptions = command.MaxRedemptions,
            TimesRedeemed = 0,
            ExpiresAt = command.ExpiresAt,
        };
        _dbContext.Discounts.Add(discount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(discount);
    }

    public async Task<DiscountComputation> ComputeAsync(string? code, decimal subtotal, string currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new DiscountComputation(null, null, 0m);
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var discount = await _dbContext.Discounts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == code, cancellationToken)
            ?? throw new InvalidOperationException($"Coupon '{code}' was not found.");

        if (!discount.IsActive)
        {
            throw new InvalidOperationException($"Coupon '{code}' is not active.");
        }
        if (discount.ExpiresAt is { } expiry && expiry <= _clock.UtcNow)
        {
            throw new InvalidOperationException($"Coupon '{code}' has expired.");
        }
        if (discount.MaxRedemptions is { } max && discount.TimesRedeemed >= max)
        {
            throw new InvalidOperationException($"Coupon '{code}' has reached its redemption limit.");
        }

        decimal amount;
        if (discount.Kind == DiscountKinds.Percentage)
        {
            amount = Math.Round(subtotal * (discount.Value / 100m), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            if (!string.Equals(discount.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Coupon '{code}' applies to {discount.Currency}, not {currency}.");
            }
            amount = discount.Value;
        }

        // Never discount below zero.
        amount = Math.Min(amount, subtotal);
        return new DiscountComputation(discount.Id, discount.Code, amount);
    }

    public async Task MarkRedeemedAsync(Guid? discountId, CancellationToken cancellationToken = default)
    {
        if (discountId is not { } id)
        {
            return;
        }
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var discount = await _dbContext.Discounts
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, cancellationToken);
        if (discount is not null)
        {
            discount.TimesRedeemed++;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static DiscountDto Map(Discount d) =>
        new(d.Id, d.Code, d.Kind, d.Value, d.Currency, d.IsActive, d.MaxRedemptions, d.TimesRedeemed, d.ExpiresAt);
}
