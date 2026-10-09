using Aonik.Commerce.Contracts.Models.Fulfilment;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

public sealed class UpdateOrderFulfilmentValidator : Validator<UpdateOrderFulfilmentCommand>
{
    public UpdateOrderFulfilmentValidator()
    {
        RuleFor(x => x.Status).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ExpectedVersion).NotNull().MaximumLength(64);
    }
}
