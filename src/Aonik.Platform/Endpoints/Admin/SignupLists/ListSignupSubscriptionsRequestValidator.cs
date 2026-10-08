using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.SharedKernel.Validation;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.Admin.SignupLists;

internal sealed class ListSignupSubscriptionsRequestValidator : Validator<ListSignupSubscriptionsRequest>
{
    public ListSignupSubscriptionsRequestValidator()
    {
        RuleFor(x => x.ListType).Must(SignupListTypes.IsKnown).WithMessage("Unknown sign-up list.");
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize();
    }
}
