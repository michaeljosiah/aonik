using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Admin.GiftCards;

public sealed class CompletePhysicalGiftCardValidator : Validator<CompletePhysicalGiftCardRequest>
{
    public CompletePhysicalGiftCardValidator()
    {
        RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(64);
    }
}
