using Aonik.Commerce.Contracts.Api.Loyalty;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Admin.Loyalty;

public sealed class AdjustLoyaltyRequestValidator : Validator<AdjustLoyaltyRequest>
{
    public AdjustLoyaltyRequestValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.AdjustmentId).NotEmpty();
        RuleFor(x => x.Points).NotEqual(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
