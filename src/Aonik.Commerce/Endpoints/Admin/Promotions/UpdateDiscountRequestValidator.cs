using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Admin.Promotions;

public sealed class UpdateDiscountRequestValidator : Validator<UpdateDiscountRequest>
{
    public UpdateDiscountRequestValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Currency).MaximumLength(3);
        RuleFor(x => x.ExpectedVersion).NotNull().MaximumLength(64);
    }
}
