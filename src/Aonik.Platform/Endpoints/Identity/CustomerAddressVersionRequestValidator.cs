using Aonik.Platform.Contracts.Models.Identity;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class CustomerAddressVersionRequestValidator : Validator<CustomerAddressVersionRequest>
{
    public CustomerAddressVersionRequestValidator()
    {
        // Missing/invalid proof is a service-level concurrency conflict; bound transport input.
        RuleFor(x => x.ExpectedVersion).MaximumLength(1024);
    }
}
