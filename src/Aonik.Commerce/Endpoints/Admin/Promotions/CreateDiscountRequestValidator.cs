using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Admin.Promotions;

public sealed class CreateDiscountRequestValidator : Validator<CreateDiscountRequest>
{
    public CreateDiscountRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Currency).MaximumLength(3);
    }
}
