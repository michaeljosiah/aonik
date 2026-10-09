using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Services.Identity;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class CustomerAddressWriteValidator : Validator<CustomerAddressWrite>
{
    public CustomerAddressWriteValidator()
    {
        RuleFor(x => x).Custom((request, context) =>
        {
            foreach (var failure in new CustomerAddressInputValidator().Validate(CustomerAddressInputValidator.Normalize(request)).Errors)
                context.AddFailure(failure);
        });
    }
}
