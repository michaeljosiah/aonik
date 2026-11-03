using Aonik.Finance.Contracts.Api.Remittance;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class ConfirmOperatorRemittanceRequestValidator : Validator<ConfirmOperatorRemittanceRequest>
{
    public ConfirmOperatorRemittanceRequestValidator()
    {
        RuleFor(request => request.Transfer).NotNull().SetValidator(new ConfirmRemittanceRequestValidator());
        RuleFor(request => request.ConfirmExecution).Equal(true).WithMessage("Explicit transfer execution confirmation is required.");
    }
}
