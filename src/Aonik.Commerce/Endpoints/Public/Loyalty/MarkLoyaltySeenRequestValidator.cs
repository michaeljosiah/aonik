using Aonik.Commerce.Contracts.Api.Loyalty;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Public.Loyalty;

public sealed class MarkLoyaltySeenRequestValidator : Validator<MarkLoyaltySeenRequest>
{
    public MarkLoyaltySeenRequestValidator()
        => RuleFor(x => x.Mark).InclusiveBetween(0, long.MaxValue / 500);
}
