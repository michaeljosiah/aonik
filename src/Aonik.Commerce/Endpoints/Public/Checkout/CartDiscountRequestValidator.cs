using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class CartDiscountRequestValidator : Validator<CartDiscountRequest>
{
    public CartDiscountRequestValidator()
        => RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
}
